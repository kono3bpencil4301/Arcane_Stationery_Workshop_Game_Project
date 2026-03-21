class_name Stats
extends Node

signal no_health
signal health_changed(value: int)

@export var max_health: int = 50
var health: int = 50:
	set(value):
		var clamped_value := clampi(value, 0, max_health)
		if health == clamped_value:
			return
		health = clamped_value
		health_changed.emit(health)

		if health <= 0:
			no_health.emit()

func _ready() -> void:
	health = max_health
