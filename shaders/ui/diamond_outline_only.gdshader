shader_type canvas_item;

void fragment() {
    vec4 source = texture(TEXTURE, UV);
    float highest = max(source.r, max(source.g, source.b));
    float lowest = min(source.r, min(source.g, source.b));
    float colored_edge = step(0.12, highest - lowest);

    // The source outline texture also contains an opaque gray center. Remove
    // only that neutral center and keep the original colored pixel outline.
    COLOR = vec4(source.rgb, source.a * colored_edge);
}
