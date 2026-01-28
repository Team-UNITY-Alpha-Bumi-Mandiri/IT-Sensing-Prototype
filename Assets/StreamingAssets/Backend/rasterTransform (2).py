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
                    # RVI: SWIR / NIR (Based on reference implementation)
                    swir, err1 = read_band('swir')
                    nir, err2 = read_band('nir')
                    if err1 or err2: raise ValueError(err1 or err2)
                    output_data = swir / (nir + 1e-6)

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

                elif self.algorithm == 'NDWI':
                    # NDWI: (Green - NIR) / (Green + NIR)
                    green, err1 = read_band('green')
                    nir, err2 = read_band('nir')
                    if err1 or err2: raise ValueError(err1 or err2)
                    output_data = (green - nir) / (green + nir + 1e-6)

                elif self.algorithm == 'TVI':
                    # TVI: sqrt(NDVI) + 0.5
                    # First calc NDVI = (NIR - Red) / (NIR + Red)
                    red, err1 = read_band('red')
                    nir, err2 = read_band('nir')
                    if err1 or err2: raise ValueError(err1 or err2)
                    ndvi = (nir - red) / (nir + red + 1e-6)
                    # TVI usually requires NDVI to be positive for sqrt, but if NDVI < 0, sqrt is nan.
                    # Reference implementation: ndvi.sqrt().add(0.5)
                    # So we clip NDVI to 0? Or just let it be nan?
                    # GEE .sqrt() on negative returns Masked/NaN usually. 
                    # Let's replace negative NDVI with 0 before sqrt to avoid warnings/errors if strict,
                    # or just let numpy handle it (returns nan and warning).
                    # For safety matching typical output:
                    ndvi_safe = np.where(ndvi < 0, 0, ndvi) 
                    output_data = np.sqrt(ndvi_safe) + 0.5
                
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
    """Buat PNG preview dari file TIF"""
    try:
        with rasterio.open(tif_path) as src:
            data = src.read()
            
            # Normalisasi untuk display
            if algo == 'TCI':
                # RGB - Normalize each band to 0-255 based on min/max of the image or standard reflectance
                # Simple min-max across all bands for color consistency, or per band?
                # Usually per-band min-max is better for visualization of raw data
                display_data = np.zeros(data.shape, dtype=np.uint8)
                for i in range(data.shape[0]):
                    band = data[i]
                    p2, p98 = np.percentile(band, (2, 98))
                    if p98 - p2 > 0:
                        norm = (band - p2) / (p98 - p2) * 255
                    else:
                        norm = band * 0
                    display_data[i] = np.clip(norm, 0, 255)
                
                # Transpose (C, H, W) -> (H, W, C)
                img_array = np.transpose(display_data, (1, 2, 0))
                
            else:
                # Single Band Index
                band = data[0]
                # Normalisasi Min-Max untuk visualisasi (Grayscale)
                # Matches GEE logic: getThumbUrl with min/max from reduceRegion
                min_val = np.nanmin(band)
                max_val = np.nanmax(band)
                
                if max_val - min_val > 0:
                    norm = (band - min_val) / (max_val - min_val) * 255
                else:
                    norm = band * 0
                
                img_array = np.clip(norm, 0, 255).astype(np.uint8)
                
                # Default to Grayscale (No Colormap)
                # img_array is already suitable for grayscale image creation
            
            # Resize if too large
            h, w = img_array.shape[:2]
            if max(h, w) > max_size:
                scale = max_size / max(h, w)
                new_h, new_w = int(h * scale), int(w * scale)
                img_array = cv2.resize(img_array, (new_w, new_h))
            
            # Save
            if algo == 'TCI':
                img = Image.fromarray(img_array) # RGB
            else:
                img = Image.fromarray(img_array) # Grayscale (L mode inferred from shape or explicit)

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
        'NDVI', 'NDTI', 'NDBI', 'NGRDI', 'RVI', 'SAVI', 'EVI', 
        'GNDVI', 'ARVI', 'MSAVI', 'TCI', 'CLGREEN', 'NDWI', 'TVI'
    ], help='Algorithm to apply')
    
    parser.add_argument('--input', required=True, help='Input Multiband TIFF')
    
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
        
        # Assume Standard Full Stack (1-based)
        # B1(Coastal), B2(Blue), B3(Green), B4(Red), B5(NIR), B6(SWIR1), B7(SWIR2)
        set_bands(r=4, g=3, b=2, n=5, s=6)

    elif 'LE07' in filename or ('LANDSAT' in filename.upper() and '7' in filename):
        detected_platform = "Landsat 7"
        # Landsat 7 ETM+ Standard:
        # B1(Blue), B2(Green), B3(Red), B4(NIR), B5(SWIR1), B7(SWIR2)
        set_bands(r=3, g=2, b=1, n=4, s=5)

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
