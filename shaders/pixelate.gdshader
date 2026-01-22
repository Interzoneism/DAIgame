shader_type canvas_item;

// Grab screen texture, force "nearest" filter so it doesn't blur
uniform sampler2D screen_texture : hint_screen_texture, filter_nearest;

// The size of your "big pixels"
uniform float pixel_size : hint_range(1.0, 64.0) = 4.0;

void fragment() {
    // 1. Convert current UV to absolute pixel coordinates (e.g., 1920 x 1080)
    // SCREEN_UV goes from 0 to 1. SCREEN_PIXEL_SIZE is (1/width, 1/height).
    // Dividing them gives us the actual pixel X/Y position on screen.
    vec2 pixel_pos = SCREEN_UV / SCREEN_PIXEL_SIZE;

    // 2. Snap that position to your grid size using floor()
    vec2 snapped_pos = floor(pixel_pos / pixel_size) * pixel_size;

    // 3. Convert back to UV (0 to 1) to read the texture
    // We add (pixel_size / 2.0) to sample from the CENTER of the pixel block
    // This prevents weird jittering artifacts when the camera moves.
    vec2 new_uv = (snapped_pos + (pixel_size / 2.0)) * SCREEN_PIXEL_SIZE;

    COLOR = texture(screen_texture, new_uv);
}