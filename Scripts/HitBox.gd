extends Area2D
class_name HitBox

@export var damage: int = 1

func _ready() -> void:
	area_entered.connect(_on_area_entered)

func _on_area_entered(area: Area2D) -> void:
	if area is HurtBox:
		var hurtbox := area as HurtBox
		print("[HitBox] hit:", hurtbox.name)
		hurtbox.hurt.emit(self)
