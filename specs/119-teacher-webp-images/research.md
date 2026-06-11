# Research: Teacher Image WebP Conversion

This document details the architectural decisions and technology choices for implementing the WebP conversion on teacher image uploads.

## Technology Choice: Client-Side vs Server-Side Compression

### Option 1: Server-Side Conversion (C# Backend)
- **Method**: Install `SixLabors.ImageSharp` package, decode base64, load image buffer, resize, compress, and save as WebP.
- **Pros**: Defends against bypasses (e.g. direct API requests sending raw PNG/JPEG).
- **Cons**: 
  - CPU-heavy. Compression of 5MB-10MB photos on a single-core VPS can block the thread pool.
  - Bandwidth consumption. Large files are uploaded raw, wasting user bandwidth.
  - Requires native dependencies or memory-intensive libraries.

### Option 2: Client-Side Compression (HTML5 Canvas)
- **Method**: Use `<canvas>` in the browser to resize images (e.g., max 800x800) and compress them into WebP (`canvas.toDataURL('image/webp', 0.8)`).
- **Pros**:
  - Extremely fast. Offloads CPU cycles to client hardware.
  - Drastic upload size reduction. Reduces upload payloads from megabytes to ~50KB.
  - No external packages needed on the backend.
- **Cons**: Requires browser support (supported in 100% of modern browsers as of 2026).

### Option 3: Hybrid Approach (Recommended)
- **Frontend** performs the primary Canvas resizing and WebP compression, renaming the extension to `.webp` during upload.
- **Backend** verifies the base64 MIME type header (e.g. `data:image/webp`) and overrides the saved filename's extension to `.webp` if it starts with WebP, preventing mismatches.

We have selected **Option 3 (Hybrid Approach)** for maximum performance and reliability.

## Browser Support Check
Canvas-to-WebP export is supported by:
- Google Chrome (since v9)
- Microsoft Edge (since v12)
- Mozilla Firefox (since v96)
- Safari (since v16 - 2022)
- iOS Safari (since v16 - 2022)

Since our users run on modern devices, client-side WebP export will work out-of-the-box. Fallback to JPEG is implemented in `image-compressor.ts` in case of legacy environments.
