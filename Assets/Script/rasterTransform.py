import argparse
import json
import os
from datetime import datetime
import rasterio
import numpy as np
from PIL import Image
import cv2

class Data():
    def __init__(self, name, algorithm):
        # --- MAPPING VARIABLE ---
        self.prefix_name = name        # -n: Nama Depan File
        self.algorithm = algorithm     # Algorithm name
        self.base_folder = 'TRANSFORM' # Base folder for output
        
        # Use same name for prefix and output folder
        self.output_folder_name = os.path.join(self.base_folder, self.prefix_name)
        
        # Setup Output
        self.set_ymdhms()
        
        self.folder_output = f'{self.output_folder_name}'
        if not os.path.exists(self.folder_output):
            os.makedirs(self.folder_output, exist_ok=True)

        self.filename = f'{self.prefix_name}_{self.algorithm}_{self.ymdhms}.tif'
        self.output_final_path = os.path.join(self.folder_output, self.filename)
        
        # PNG preview filename
        self.png_filename = f'{self.prefix_name}_{self.algorithm}_{self.ymdhms}_preview.png'
        self.png_path = os.path.join(self.folder_output, self.png_filename)

        self.status = 'running'
        self.messages = 'Initializing...'

    def set_ymdhms(self):
        self.now = datetime.now()
        self.ymdhms = datetime.now().strftime('%y%m%d%H%M%S')

    def run(self, input_path, band_indices):
        try:
            # 1. VALIDASI INPUT
            if not os.path.exists(input_path):
                self.status = 'failed'
                self.messages = f'Input file not found: {input_path}'
                self._print_result()
                return

            # 2. EKSEKUSI PROSES TRANSFORM
            result = self.process_transform(input_path, self.output_final_path, band_indices)
            
            if result:
                self.status = 'success'
                self.messages = f'{self.algorithm} calculation successful'
                
                # Buat PNG preview dan dapatkan bounds
                create_preview_png(self.output_final_path, self.png_path, self.algorithm)
                bounds = get_bounds(self.output_final_path)
                
                # OUTPUT JSON
                self._print_result(bounds)
            else:
                self.status = 'failed'
                # messages sudah di-set di dalam process_transform jika ada error spesifik
                if self.messages == 'Initializing...': 
                    self.messages = 'Transformation failed'
                self._print_result()

        except Exception as e:
            self.status = 'failed'
            self.messages = str(e)
            self._print_result()

    def _print_result(self, bounds=None):
        result = {
            'status': self.status,
            'messages': self.messages,
            'filename': self.filename if self.status == 'success' else None,
            'path': self.output_final_path if self.status == 'success' else None,
            'preview_png': self.png_filename if self.status == 'success' else None,
            'bounds': bounds if bounds else {},
            'algo': self.algorithm
        }
        print(json.dumps(result))

    def process_transform(self, input_path, output_path, band_indices):
        try:
            with rasterio.open(input_path) as src:
                profile = src.profile.copy()
                
                # Helper to read band by name (mapped to index) or explicit index
                def read_band(name):
                    idx = band_indices.get(name)
                    if idx is None:
                        return None, f"Band '{name}' index not provided"
                    if idx > src.count:
                        return None, f"Band index {idx} out of range (max {src.count})"
                    return src.read(idx).astype(np.float32), None

                # Calculate specific algorithm
                output_data = None
                
                # --- ALGORITHMS ---
                
                if self.algorithm == 'NDVI':
                    red, err1 = read_band('red')
                    nir, err2 = read_band('nir')
                    if err1 or err2: raise ValueError(err1 or err2)
                    output_data = (nir - red) / (nir + red + 1e-6)

                elif self.algorithm == 'NDTI':
                    # NDTI (Turbidity): (Red - Green) / (Red + Green)
                    red, err1 = read_band('red')
                    green, err2 = read_band('green')
                    if err1 or err2: raise ValueError(err1 or err2)
                    output_data = (red - green) / (red + green + 1e-6)

                elif self.algorithm == 'NDBI':
                    # NDBI: (SWIR - NIR) / (SWIR + NIR)
                    swir, err1 = read_band('swir')
                    nir, err2 = read_band('nir')
                    if err1 or err2: raise ValueError(err1 or err2)
                    output_data = (swir - nir) / (swir + nir + 1e-6)
                
                elif self.algorithm == 'NGRDI':
                    # NGRDI: (Green - Red) / (Green + Red)
                    green, err1 = read_band('green')
                    red, err2 = read_band('red')
                    if err1 or err2: raise ValueError(err1 or err2)
                    output_data = (green - red) / (green + red + 1e-6)
                
                elif self.algorithm == 'RVI':
                    # RVI: NIR / Red
                    nir, err1 = read_band('nir')
                    red, err2 = read_band('red')
                    if err1 or err2: raise ValueError(err1 or err2)
                    output_data = nir / (red + 1e-6)

                elif self.algorithm == 'SAVI':
                    # SAVI: ((NIR - Red) / (NIR + Red + L)) * (1 + L), L=0.5
                    nir, err1 = read_band('nir')
                    red, err2 = read_band('red')
                    if err1 or err2: raise ValueError(err1 or err2)
                    L = 0.5
                    output_data = ((nir - red) / (nir + red + L)) * (1 + L)

                elif self.algorithm == 'EVI':
                    # EVI: 2.5 * ((NIR - Red) / (NIR + 6*Red - 7.5*Blue + 1))
                    nir, err1 = read_band('nir')
                    red, err2 = read_band('red')
                    blue, err3 = read_band('blue')
                    if err1 or err2 or err3: raise ValueError(err1 or err2 or err3)
                    output_data = 2.5 * ((nir - red) / (nir + 6 * red - 7.5 * blue + 1 + 1e-6))

                elif self.algorithm == 'GNDVI':
                    # GNDVI: (NIR - Green) / (NIR + Green)
                    nir, err1 = read_band('nir')
                    green, err2 = read_band('green')
                    if err1 or err2: raise ValueError(err1 or err2)
                    output_data = (nir - green) / (nir + green + 1e-6)

                elif self.algorithm == 'ARVI':
                    # ARVI: (NIR - (2 * Red - Blue)) / (NIR + (2 * Red - Blue))
                    nir, err1 = read_band('nir')
                    red, err2 = read_band('red')
                    blue, err3 = read_band('blue')
                    if err1 or err2 or err3: raise ValueError(err1 or err2 or err3)
                    rb = 2 * red - blue
                    output_data = (nir - rb) / (nir + rb + 1e-6)

                elif self.algorithm == 'MSAVI':
                    # MSAVI2: (2 * NIR + 1 - sqrt((2 * NIR + 1)^2 - 8 * (NIR - Red))) / 2
                    nir, err1 = read_band('nir')
                    red, err2 = read_band('red')
                    if err1 or err2: raise ValueError(err1 or err2)
                    output_data = (2 * nir + 1 - np.sqrt(np.square(2 * nir + 1) - 8 * (nir - red))) / 2

                elif self.algorithm == 'CLGREEN':
                    # CLGREEN: (NIR / Green) - 1
                    nir, err1 = read_band('nir')
                    green, err2 = read_band('green')
                    if err1 or err2: raise ValueError(err1 or err2)
                    output_data = (nir / (green + 1e-6)) - 1
                
                elif self.algorithm == 'TCI':
                    # TCI: True Color Image (Red, Green, Blue) -> 3 Bands
                    red, err1 = read_band('red')
                    green, err2 = read_band('green')
                    blue, err3 = read_band('blue')
                    if err1 or err2 or err3: raise ValueError(err1 or err2 or err3)
                    
                    # Stack bands
                    output_data = np.stack([red, green, blue])
                    
                    # Update profile for 3 bands
                    profile.update(count=3)
                
                else:
                    raise ValueError(f"Unknown algorithm: {self.algorithm}")

                # Save Result
                if output_data is None:
                    raise ValueError("Calculation resulted in None")

                # Handle NaN/Inf
                output_data = np.nan_to_num(output_data, nan=0.0, posinf=0.0, neginf=0.0)

                # Update Profile
                if self.algorithm != 'TCI':
                    # Single band output for indices
                    profile.update(
                        dtype=rasterio.float32,
                        count=1,
                        compress='lzw'
                    )
                    # Reshape for writing if needed (1, H, W)
                    if output_data.ndim == 2:
                        output_data = output_data[np.newaxis, :, :]
                else:
                    # Multi band (TCI)
                    # Ensure dtype matches source or float32? usually TCI is good as uint8 for display, 
                    # but let's keep float32 for consistency or source dtype.
                    # Actually, keeping it float32 is safest for intermediate.
                    profile.update(dtype=rasterio.float32, compress='lzw')

                with rasterio.open(output_path, 'w', **profile) as dst:
                    dst.write(output_data.astype(rasterio.float32))

            return True

        except Exception as e:
            self.messages = str(e)
            return False

def create_preview_png(tif_path, png_path, algo, max_size=1024):
    """Buat PNG preview dari file TIF dengan support Transparency"""
    try:
        with rasterio.open(tif_path) as src:
            data = src.read()
            
            # Normalisasi untuk display
            if algo == 'TCI':
                # RGB - Normalize each band
                display_data = np.zeros(data.shape, dtype=np.uint8)
                for i in range(min(data.shape[0], 3)):
                    band = data[i]
                    # Handle NaNs
                    valid_mask = np.isfinite(band)
                    if not np.any(valid_mask):
                        continue
                        
                    p2, p98 = np.percentile(band[valid_mask], (2, 98))
                    
                    norm = np.zeros_like(band)
                    if p98 - p2 > 0:
                        norm[valid_mask] = (band[valid_mask] - p2) / (p98 - p2) * 255
                    
                    display_data[i] = np.clip(norm, 0, 255)
                
                # Transpose (C, H, W) -> (H, W, C)
                img_array = np.transpose(display_data[:3], (1, 2, 0))
                img = Image.fromarray(img_array) # RGB (No Alpha for TCI yet)
                
            else:
                # Single Band Index
                band = data[0].astype(np.float32)
                
                # Create Mask for Valid Data (Not NaN, Not Inf)
                mask = np.isfinite(band)
                
                # If empty
                if not np.any(mask):
                    print("Warning: Image contains no valid data.")
                    return False

                valid_data = band[mask]
                
                # Percentile on valid data only
                p2, p98 = np.percentile(valid_data, (2, 98))
                
                # Normalize
                norm = np.zeros_like(band)
                if p98 - p2 > 0:
                    norm[mask] = (band[mask] - p2) / (p98 - p2) * 255
                
                img_gray = np.clip(norm, 0, 255).astype(np.uint8)
                
                # Apply Colormap (Returns BGR)
                img_color = cv2.applyColorMap(img_gray, cv2.COLORMAP_JET)
                
                # Create Alpha Channel
                # 0 for Invalid/NaN, 255 for Valid
                alpha = np.zeros_like(band, dtype=np.uint8)
                alpha[mask] = 255
                
                # Convert BGR to RGB and Add Alpha
                b, g, r = cv2.split(img_color)
                img_rgba = cv2.merge((r, g, b, alpha))
                
                # Resize if too large
                h, w = img_rgba.shape[:2]
                if max(h, w) > max_size:
                    scale = max_size / max(h, w)
                    new_h, new_w = int(h * scale), int(w * scale)
                    img_rgba = cv2.resize(img_rgba, (new_w, new_h), interpolation=cv2.INTER_NEAREST)
                
                img = Image.fromarray(img_rgba)

            img.save(png_path, 'PNG')
            return True
            
    except Exception as e:
        print(f"Gagal membuat PNG preview: {str(e)}")
        return False

def get_bounds(tif_path):
    """Dapatkan bounds dari file TIF"""
    try:
        with rasterio.open(tif_path) as src:
            bounds = src.bounds
            return {
                "north": float(bounds.top),
                "south": float(bounds.bottom),
                "west": float(bounds.left),
                "east": float(bounds.right)
            }
    except Exception as e:
        return None

def main():
    parser = argparse.ArgumentParser(description='Raster Transformation Tool')
    parser.add_argument('-n', required=True, help='Output Prefix Name')
    parser.add_argument('--algo', required=True, choices=[
        'NDTI', 'NDVI', 'NDBI', 'NGRDI', 'RVI', 'SAVI', 'EVI', 
        'GNDVI', 'ARVI', 'MSAVI', 'TCI', 'CLGREEN'
    ], help='Algorithm to apply')
    
    parser.add_argument('--input', required=True, help='Input Multiband TIFF')
    
    args = parser.parse_args()
    args = parser.parse_args()

    args = parser.parse_args()

    # --- SATELLITE & BAND PARSER ---
    filename = os.path.basename(args.input)
    band_indices = {}
    
    # Defaults
    detected_platform = "Unknown"
    
    # Helper to set bands
    def set_bands(r, g, b, n, s):
        band_indices['red'] = r
        band_indices['green'] = g
        band_indices['blue'] = b
        band_indices['nir'] = n
        band_indices['swir'] = s

    # Logic
    if 'LC08' in filename or 'LC09' in filename or 'LANDSAT' in filename.upper():
        detected_platform = "Landsat 8/9"
        
        # Check for Specific Stack: B2-B7 (6 bands)
        # B2(Blue), B3(Green), B4(Red), B5(NIR), B6(SWIR1), B7(SWIR2)
        if 'B2-B7' in filename or 'stack_B2-B7' in filename:
            # File Band 1 = L8 Band 2 (Blue)
            # File Band 2 = L8 Band 3 (Green)
            # File Band 3 = L8 Band 4 (Red)
            # File Band 4 = L8 Band 5 (NIR)
            # File Band 5 = L8 Band 6 (SWIR1)
            # File Band 6 = L8 Band 7 (SWIR2)
            set_bands(r=3, g=2, b=1, n=4, s=5)
            
        else:
            # Assume Standard Full Stack (1-based)
            # B1(Coastal), B2(Blue), B3(Green), B4(Red), B5(NIR), B6(SWIR1), B7(SWIR2)
            set_bands(r=4, g=3, b=2, n=5, s=6)

    elif 'S2' in filename or 'SENTINEL' in filename.upper():
        detected_platform = "Sentinel-2"
        # Standard S2 L2A/L1C stack usually:
        # B1, B2, B3, B4, B5, B6, B7, B8, B8A, B9, B10, B11, B12
        # Blue=2, Green=3, Red=4, NIR=8, SWIR=11
        set_bands(r=4, g=3, b=2, n=8, s=11)
        
    else:
        # Fallback: Try reading metadata if filename fails, or default to 1-5 mapping
        try:
            with rasterio.open(args.input) as src:
                descriptions = [d.lower() if d else "" for d in src.descriptions]
                for i, desc in enumerate(descriptions):
                    idx = i + 1
                    if 'red' in desc: band_indices['red'] = idx
                    elif 'green' in desc: band_indices['green'] = idx
                    elif 'blue' in desc: band_indices['blue'] = idx
                    elif 'nir' in desc or 'near infrared' in desc: band_indices['nir'] = idx
                    elif 'swir' in desc: band_indices['swir'] = idx
        except:
            pass
            
        if not band_indices:
            # Final Fallback to Assumption if no platform detected and no metadata
            # Assume: 1=Red, 2=Green, 3=Blue, 4=NIR, 5=SWIR
            set_bands(r=1, g=2, b=3, n=4, s=5)
            detected_platform = "Generic (Default Mapping)"

    print(json.dumps({
        'status': 'info',
        'messages': f'Detected Platform: {detected_platform}. Band Mapping used: {band_indices}',
        'algo': args.algo
    }))


    
    data = Data(name=args.n, algorithm=args.algo)
    data.run(input_path=args.input, band_indices=band_indices)

if __name__ == '__main__':
    main()
