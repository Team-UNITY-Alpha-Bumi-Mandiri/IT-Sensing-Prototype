import argparse
import json
import os
import sys
from datetime import datetime
import rasterio
import numpy as np
from PIL import Image
import cv2

class Data:
    def __init__(self, name, formula):
        self.prefix_name = name
        self.formula = formula
        self.base_folder = 'Calculator'
        
        # Use same name for prefix and output folder
        self.output_folder_name = os.path.join(self.base_folder, self.prefix_name)
        
        self.set_ymdhms()
        
        self.folder_output = f'{self.output_folder_name}'
        if not os.path.exists(self.folder_output):
            os.makedirs(self.folder_output)

        self.filename = f'{self.prefix_name}_custom_{self.ymdhms}.tif'
        self.output_final_path = os.path.join(self.folder_output, self.filename)
        
        self.png_filename = f'{self.prefix_name}_custom_{self.ymdhms}_preview.png'
        self.png_path = os.path.join(self.folder_output, self.png_filename)

        self.status = 'running'
        self.messages = 'Initializing...'

    def set_ymdhms(self):
        self.now = datetime.now()
        self.ymdhms = datetime.now().strftime('%y%m%d%H%M%S')

    def run(self, input_path):
        try:
            if not os.path.exists(input_path):
                raise FileNotFoundError(f"Input file not found: {input_path}")

            with rasterio.open(input_path) as src:
                # Prepare context for eval
                context = {'np': np}
                
                # Dynamic band loading: b1, b2, ...
                # We load all bands available in the image
                for i in range(1, src.count + 1):
                    # Read band as float32 for calculation
                    band_data = src.read(i).astype(np.float32)
                    context[f'b{i}'] = band_data

                # --- VALIDATE FORMULA VARIABLES ---
                # Check if formula uses bands that don't exist
                # This is a basic check. eval() will fail if variable is missing.
                
                # --- CALCULATION ---
                try:
                    # Evaluate the formula
                    # Note: We trust the user implementation/input as this is a local tool.
                    # Security warning: eval() is dangerous if input is untrusted, 
                    # but here it is a local tool executing user command.
                    
                    # 1e-6 is often used for stability in division, user might include it in formula or we can suggest it.
                    # The user provided formula logic in raster_calculator.py usually assumes GEE syntax 
                    # but here we use Python/Numpy syntax. b1 + b2 works in both.
                    
                    result = eval(self.formula, {"__builtins__": None}, context)
                    
                except Exception as eval_err:
                    raise ValueError(f"Formula evaluation failed: {eval_err}")

                # Handle result
                if result is None:
                    raise ValueError("Calculation resulted in None")

                # Handle NaN/Inf
                result = np.nan_to_num(result, nan=0.0, posinf=0.0, neginf=0.0)

                # Prepare profile for output
                profile = src.profile.copy()
                profile.update(
                    dtype=rasterio.float32,
                    count=1,
                    compress='lzw'
                )
                
                # Reshape if necessary
                if result.ndim == 2:
                    result = result[np.newaxis, :, :]

                # Write output
                with rasterio.open(self.output_final_path, 'w', **profile) as dst:
                    dst.write(result.astype(rasterio.float32))

            # Success
            self.status = 'success'
            self.messages = f'Calculation successful: {self.formula}'
            
            # Create Preview
            self.create_preview(result[0])
            
            # Get Bounds
            bounds = self.get_bounds(self.output_final_path)
            
            self._print_result(bounds)

        except Exception as e:
            self.status = 'failed'
            self.messages = str(e)
            self._print_result()

    def create_preview(self, data, max_size=1024):
        try:
            # Normalize Min-Max
            min_val = np.nanmin(data)
            max_val = np.nanmax(data)
            
            if max_val - min_val > 0:
                norm = (data - min_val) / (max_val - min_val) * 255
            else:
                norm = data * 0
            
            img_array = np.clip(norm, 0, 255).astype(np.uint8)
            
            # Resize
            h, w = img_array.shape
            if max(h, w) > max_size:
                scale = max_size / max(h, w)
                new_h, new_w = int(h * scale), int(w * scale)
                img_array = cv2.resize(img_array, (new_w, new_h))
            
            img = Image.fromarray(img_array)
            img.save(self.png_path, 'PNG')
            
        except Exception as e:
            print(f"Warning: Failed to create preview: {e}", file=sys.stderr)

    def get_bounds(self, tif_path):
        try:
            with rasterio.open(tif_path) as src:
                bounds = src.bounds
                return {
                    "north": float(bounds.top),
                    "south": float(bounds.bottom),
                    "west": float(bounds.left),
                    "east": float(bounds.right)
                }
        except Exception:
            return {}


    def _print_result(self, bounds=None):
        result = {
            'status': self.status,
            'messages': self.messages,
            'filename': self.filename if self.status == 'success' else None,
            'path': self.output_final_path if self.status == 'success' else None,
            'preview_png': self.png_filename if self.status == 'success' else None,
            'bounds': bounds if bounds else {},
            'formula': self.formula
        }
        print(json.dumps(result))

def get_bands(file_path):
    try:
        if not os.path.exists(file_path):
            print(json.dumps({'status': 'failed', 'message': f"File not found: {file_path}"}))
            return

        with rasterio.open(file_path) as src:
            bands = []
            # Try to get descriptions, fallback to Index
            for i in range(1, src.count + 1):
                desc = src.descriptions[i-1]
                if desc:
                    bands.append(desc)
                else:
                    bands.append(f"b{i}")
            
            print(json.dumps({'status': 'success', 'bands': bands, 'count': src.count, 'type': 'GeoTIFF'}))

    except Exception as e:
        print(json.dumps({'status': 'failed', 'message': f"Failed to read bands: {str(e)}"}))

def main():
    parser = argparse.ArgumentParser(description='Standalone Raster Calculator')
    parser.add_argument('-i', '--input', required=False, help='Input Image Path (TIFF)')
    parser.add_argument('-f', '--formula', required=False, help='Formula (e.g., "(b5-b4)/(b5+b4)")')
    parser.add_argument('-n', '--name', required=False, help='Output Prefix Name')
    parser.add_argument('-b', '--bands', required=False, help='Check bands in file (Input Path)')
    
    args = parser.parse_args()

    if args.bands:
        get_bands(args.bands)
        return

    if not all([args.input, args.formula, args.name]):
        parser.error("Arguments -i, -f, and -n are required for calculation.")
    
    # Instantiate and Run
    data = Data(args.name, args.formula)
    data.run(args.input)

if __name__ == '__main__':
    main()
