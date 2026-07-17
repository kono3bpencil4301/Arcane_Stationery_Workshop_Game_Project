shader_type canvas_item;

uniform float ratio : hint_range(0.0, 1.0) = 0.0;
uniform vec4 shadow_color : source_color = vec4(0.03, 0.04, 0.10, 0.82);

void fragment() {
    vec2 centered = UV - vec2(0.5);
    float angle = atan(centered.y, centered.x);
    float counter_clockwise = fract((-(angle + PI * 0.5)) / TAU);
    float inside = step(counter_clockwise, ratio);
    COLOR = shadow_color * inside;
}
