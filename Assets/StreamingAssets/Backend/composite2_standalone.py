import argparse
import rasterio
import numpy as np
import os
import sys
from PIL import Image

# --------------------------------------------------
# Save preview as PNG
# --------------------------------------------------
def save_preview_png(rgb_array, output_tif_path):
    try:
        # Normalize logic:
        # rgb_array shape is (3, H, W)
        # We need to normalize each channel to 0-255 independently for better visualization
        
        c, h, w = rgb_array.shape
        rgb_norm = np.zeros((h, w, 3), dtype=np.uint8)
        
        for i in range(3):
            band = rgb_array[i]
            
            # Handle NaN/Inf
            band = np.nan_to_num(band, nan=0.0, posinf=0.0, neginf=0.0)
            
            # Use Percentile Stretch (2-98%) for better visual contrast
            # Min-Max is too sensitive to outliers
            
            # Check if band has any data
            if np.all(band == 0):
                rgb_norm[:, :, i] = 0
                continue
                
            p2, p98 = np.percentile(band, (2, 98))
            
            if p98 - p2 > 1e-6:
                band_norm = (band - p2) / (p98 - p2)
            else:
                # If range is zero, try min-max as fallback
                min_val = np.min(band)
                max_val = np.max(band)
                if max_val - min_val > 1e-6:
                    band_norm = (band - min_val) / (max_val - min_val)
                else:
                    band_norm = np.zeros_like(band)
                
            # Clip to 0-1 range after stretching
            band_norm = np.clip(band_norm, 0, 1)
                
            rgb_norm[:, :, i] = (band_norm * 255).astype(np.uint8)
            
        # Create Mask for Transparency
        # Assuming 0,0,0 is nodata/background
        # Or better, check original data for nodata value if available, but for now simple sum check
        
        img = Image.fromarray(rgb_norm)
        
        # Add Alpha Channel? (Optional, if user wants transparency for nodata)
        # alpha = np.sum(rgb_norm, axis=2) > 0
        # alpha = (alpha * 255).astype(np.uint8)
        # img.putalpha(Image.fromarray(alpha))
        
        # Create preview filename
        base, _ = os.path.splitext(output_tif_path)
        preview_path = f"{base}_preview.png"
        
        # Resize for thumbnail (max 1024px)
        img.thumbnail((1024, 1024))
        img.save(preview_path)
        print(f"Preview generated at: {preview_path}")
        return preview_path
    except Exception as e:
        print(f"Warning: Failed to create preview PNG: {e}")
        return None

# --------------------------------------------------
# Band info (optional, for inspection/debug)
# --------------------------------------------------
def get_band_info(input_tif):
    bands = []

    with rasterio.open(input_tif) as src:
        for i in range(1, src.count + 1):
            desc = src.descriptions[i - 1]
            if not desc or desc.strip() == "":
                desc = f"Band {i}"
            bands.append((i, desc))

    return bands


# --------------------------------------------------
# Optional stretch for visualization
# --------------------------------------------------
def stretch_band(band, p_low=2, p_high=98):
    band = band.astype("float32")
    low, high = np.percentile(band, (p_low, p_high))

    if high - low == 0:
        return band

    band = (band - low) / (high - low)
    return np.clip(band, 0, 1)


# --------------------------------------------------
# Core composite logic
# --------------------------------------------------
def composite_rgb_from_single_tif(
    input_tif,
    r_band,
    g_band,
    b_band,
    output_tif,
    stretch=False
):
    if not os.path.exists(input_tif):
        raise FileNotFoundError("Input TIFF not found")

    if len({r_band, g_band, b_band}) < 3:
        raise ValueError("R, G, and B must be different bands")

    with rasterio.open(input_tif) as src:
        band_count = src.count

        for b in (r_band, g_band, b_band):
            if b < 1 or b > band_count:
                raise ValueError(
                    f"Band {b} is invalid (file has {band_count} bands)"
                )

        r = src.read(r_band)
        g = src.read(g_band)
        b = src.read(b_band)

        if stretch:
            r = stretch_band(r)
            g = stretch_band(g)
            b = stretch_band(b)

        # ---- COMPOSITE LINE ----
        rgb = np.stack([r, g, b])

        profile = src.profile
        profile.update(
            count=3,
            dtype=rgb.dtype
        )

        # Handle PNG output
        if output_tif.lower().endswith(".png"):
            profile.update(driver="PNG")
            # PNG typically requires uint8 or uint16. 
            # If we stretched (float32 0-1), we should convert to uint8.
            if rgb.dtype == 'float32' or rgb.dtype == 'float64':
                rgb = (rgb * 255).astype('uint8')
                profile.update(dtype='uint8')
            # If original was not stretched (e.g. uint16), PNG supports uint16, 
            # but for visualization uint8 is often preferred. 
            # We'll leave it unless it's float.

    with rasterio.open(output_tif, "w", **profile) as dst:
        dst.write(rgb)
        
    # Generate Preview
    preview_file = save_preview_png(rgb, output_tif)
    if preview_file:
        print(f"Preview: {preview_file}")


# --------------------------------------------------
# Argument parser (standalone mode)
# --------------------------------------------------
def parse_args():
    parser = argparse.ArgumentParser(
        description="Create RGB composite from a single multiband GeoTIFF"
    )

    parser.add_argument(
        "--input",
        required=True,
        help="Input multiband GeoTIFF"
    )

    parser.add_argument(
        "--r",
        type=int,
        required=False,
        help="Band number for RED (1-based)"
    )

    parser.add_argument(
        "--g",
        type=int,
        required=False,
        help="Band number for GREEN (1-based)"
    )

    parser.add_argument(
        "--b",
        type=int,
        required=False,
        help="Band number for BLUE (1-based)"
    )

    parser.add_argument(
        "--output",
        required=False,
        help="Output RGB GeoTIFF"
    )

    parser.add_argument(
        "--stretch",
        action="store_true",
        help="Apply percentile stretch (recommended for display)"
    )

    parser.add_argument(
        "--list-bands",
        action="store_true",
        help="List available bands and exit"
    )

    return parser.parse_args()


# --------------------------------------------------
# Main entry point
# --------------------------------------------------
def main():
    args = parse_args()

    if not os.path.exists(args.input):
        print(f"ERROR: Input file not found: {args.input}")
        sys.exit(1)

    # Optional band listing
    if args.list_bands:
        bands = get_band_info(args.input)
        print("Available bands:")
        for idx, label in bands:
            print(f"{idx}: {label}")
        sys.exit(0)

    try:
        composite_rgb_from_single_tif(
            input_tif=args.input,
            r_band=args.r,
            g_band=args.g,
            b_band=args.b,
            output_tif=args.output,
            stretch=args.stretch
        )
    except Exception as e:
        print(f"ERROR: {e}")
        sys.exit(1)

    print("RGB composite created successfully")
    print(f"Output: {args.output}")


if __name__ == "__main__":
    main()
