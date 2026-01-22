shader_type canvas_item;

// --- PIXEL SIZE SETTINGS ---
// This controls the "chunkiness" of the pixels. 
// Set this to your texture's actual width (e.g. 32) for a 1:1 look.
uniform float target_width : hint_range(1.0, 512.0) = 32.0;

// --- BOUNDARIES ---
uniform float top_limit : hint_range(0.0, 1.0) = 0.35;
uniform float bottom_limit : hint_range(0.0, 1.0) = 0.65;

// --- SQUISH FACTORS ---
uniform float top_squish : hint_range(0.001, 1.0) = 0.4;
uniform float bottom_squish : hint_range(0.001, 1.0) = 0.4;

void fragment() {
	// 1. DEFINE THE PIXEL GRID
	// We want square pixels, so we calculate the grid height based on aspect ratio.
	vec2 tex_size = vec2(textureSize(TEXTURE, 0));
	float aspect = tex_size.x / tex_size.y;
	
	float px_w = target_width;
	float px_h = target_width / aspect;

	// 2. SNAP UVs TO THE GRID (Quantization)
	// Instead of using the smooth UV, we snap it to the center of the nearest "virtual pixel".
	// This ensures that an entire square block on screen receives the exact same logic.
	vec2 grid_uv = UV;
	grid_uv.x = floor(grid_uv.x * px_w) / px_w + (0.5 / px_w);
	grid_uv.y = floor(grid_uv.y * px_h) / px_h + (0.5 / px_h);

	// 3. APPLY SQUISH LOGIC TO THE GRID UV
	// Now we calculate which part of the texture to fetch for this square block.
	float source_uv_y = grid_uv.y;

	if (grid_uv.y > bottom_limit) {
		float delta = grid_uv.y - bottom_limit;
		source_uv_y = bottom_limit + (delta / bottom_squish);
	} 
	else if (grid_uv.y < top_limit) {
		float delta = grid_uv.y - top_limit;
		source_uv_y = top_limit + (delta / top_squish);
	}

	// 4. RENDER
	// We use the grid_uv for X and the distorted Y for the texture lookup.
	vec2 final_sample_uv = vec2(grid_uv.x, source_uv_y);

	// Safety Check: If the squish asks for pixels outside the texture, cut them off.
	if (source_uv_y < 0.0 || source_uv_y > 1.0) {
		COLOR = vec4(0.0);
	} else {
		// 'textureLod' with 0.0 forces the sharpest mipmap level.
		COLOR = textureLod(TEXTURE, final_sample_uv, 0.0);
	}
}