shader_type canvas_item;

uniform bool flip = false;

void fragment() {
    vec2 adjusted_uv = UV;

    if (flip) {
        adjusted_uv.x = 1.0 - adjusted_uv.x;
    }

    COLOR = texture(TEXTURE, adjusted_uv);
}