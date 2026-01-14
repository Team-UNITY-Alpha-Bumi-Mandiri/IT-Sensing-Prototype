import argparse
import rasterio
import numpy as np
import os
import sys


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

    with rasterio.open(output_tif, "w", **profile) as dst:
        dst.write(rgb)


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

    print("✅ RGB composite created successfully")
    print(f"➡ Output: {args.output}")


if __name__ == "__main__":
    main()
