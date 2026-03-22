class_name HurtBox
extends Area2D

signal hurt(hitbox: HitBox)

func _ready() -> void:
	if not area_entered.is_connected(_on_area_entered):
		area_entered.connect(_on_area_entered)

	print("[HurtBox ready] monitoring =", monitoring)
	print("[HurtBox ready] monitorable =", monitorable)
	print("[HurtBox ready] layer =", collision_layer, " mask =", collision_mask)

func _on_area_entered(area: Area2D) -> void:
	print("[HurtBox] area_entered:", area.name, area)

	if area is HitBox:
		print("[HurtBox] emit hurt")
		hurt.emit(area)
