# PROTOTYPE — Capture-quality contract

## Question

Can FotoHAVN use one capability-based rule for resolution, aspect ratio, orientation, and center cropping: normalize the Camera's declared orientation, require at least 1280×720 landscape JPEG output, select the highest-resolution eligible photo mode, then center-crop to the final Photo Strip slot and reject the Camera if the crop cannot supply the slot's raster dimensions?

This is disposable diagnostic code for **Choose the capture-quality contract for selected Windows cameras**. It does not judge focus, exposure, composition, or aesthetics; those remain operator judgments against the mirrored, crop-matched live preview during Camera Tuning.

## Run

```powershell
dotnet run --project prototypes/capture-quality-contract/CaptureQualityContract.csproj
```

Use `n`/`p` to move through representative Camera outputs and `s` to compare plausible final landscape slots. The displayed crop rectangle is computed in orientation-normalized, unmirrored Capture coordinates.

For the selected Camera, FotoHAVN chooses the eligible photo mode with the greatest normalized pixel count. Equal-pixel modes are broken by the number of pixels retained by the final center crop. “Maximum quality” means maximum available resolution here; FotoHAVN does not attempt to score optics, noise, sharpness, or compression quality.

The output slot sizes are diagnostic examples near the maximum printable area of one 620×1844 half-sheet. The production composer remains the source of the exact slot raster dimensions.
