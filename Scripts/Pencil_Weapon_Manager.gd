extends Node2D

@export var pencil_scene: PackedScene = preload("res://Scenes/pencil_unit.tscn")

@export var orbit_radius: float = 48.0
@export var number_of_pencil: int = 3
@export var attack_duration: float = 0.6
@export var idle_duration: float = 1.2
var group_time: float = 0.0

var pencils: Array[Node2D] = []
var base_angle: float = 0.0

func _ready() -> void:
	pencils_spawn(number_of_pencil)
	setup_pencil_cycles()
	update_pencil_layout(base_angle)

func _process(delta: float) -> void:
	group_time += delta
	
	var dir := get_global_mouse_position() - global_position
	if dir.length() > 2.0:
		base_angle = dir.angle()

	update_pencil_layout(base_angle)
	update_pencil_cycles()

func pencils_spawn(pencil_count: int) -> void:
	for pencil in pencils:
		pencil.queue_free()
	pencils.clear()

	for i in range(pencil_count):
		var pencil_instance = pencil_scene.instantiate()
		pencil_instance.name = "PencilUnit_%d" % i
		add_child(pencil_instance)
		pencils.append(pencil_instance)

func setup_pencil_cycles() -> void:
	var count := pencils.size()
	if count <= 0:
		return

	var cycle := attack_duration + idle_duration

	for i in range(count):
		var pencil = pencils[i]
		var offset := cycle / count * i

		if pencil.has_method("setup_cycle"):
			pencil.setup_cycle(attack_duration, idle_duration, offset)

func update_pencil_cycles() -> void:
	for pencil in pencils:
		if pencil.has_method("update_cycle"):
			pencil.update_cycle(group_time)

func update_pencil_layout(current_angle: float) -> void:
	var count := pencils.size()
	if count <= 0:
		return

	var step := TAU / count

	for i in range(count):
		var pencil := pencils[i]
		var angle := current_angle + step * i
		pencil.position = Vector2(orbit_radius, 0).rotated(angle)
		pencil.rotation = angle
