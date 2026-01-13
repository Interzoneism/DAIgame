shader_type canvas_item;

uniform vec4 highlight_color : source_color = vec4(1.0, 1.0, 0.0, 0.5);
uniform float alpha_threshold : hint_range(0.0, 1.0) = 0.1;

void fragment() {
    vec4 tex_color = texture(TEXTURE, UV);
    
    // Only apply highlight to pixels with alpha above threshold
    if (tex_color.a > alpha_threshold) {
        // Blend the highlight color with the original color
        COLOR = vec4(mix(tex_color.rgb, highlight_color.rgb, highlight_color.a), tex_color.a);
    } else {
        // Fully transparent pixels remain transparent
        COLOR = vec4(0.0, 0.0, 0.0, 0.0);
    }
}
