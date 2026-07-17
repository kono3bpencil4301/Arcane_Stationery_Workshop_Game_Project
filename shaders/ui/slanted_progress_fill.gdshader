shader_type canvas_item;

uniform float ratio : hint_range(0.0, 1.0) = 1.0;
uniform float fill_start : hint_range(0.0, 1.0) = 0.0;
uniform float fill_end : hint_range(0.0, 1.0) = 1.0;
uniform vec4 fill_color : source_color = vec4(1.0);

void fragment() {
    vec4 source = texture(TEXTURE, UV);
    float span = max(fill_end - fill_start, 0.0001);
    float local_progress = clamp((UV.x - fill_start) / span, 0.0, 1.0);

    if (source.a <= 0.001 || local_progress > ratio) {
        discard;
    }

    COLOR = vec4(fill_color.rgb, fill_color.a * source.a);
}
