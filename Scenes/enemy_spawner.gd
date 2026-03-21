extends Node2D

var ink_blobber_scene: PackedScene = preload("res://Assets/Enemy/basic_enemy/ink_blobber.tscn")
var enemy_counter: int = 0
# Called when the node enters the scene tree for the first time.
func _ready() -> void:
	pass # Replace with function body.


# Called every frame. 'delta' is the elapsed time since the previous frame.
func _process(_delta: float) -> void:
	pass

func _on_ink_blobber_timer_timeout() -> void:
	var ink_blobber_instance = ink_blobber_scene.instantiate() as CharacterBody2D
	add_child(ink_blobber_instance)
	var pos_Marker = $EnemyStartPositions.get_children().pick_random() as Marker2D
	ink_blobber_instance.position = pos_Marker.position
	
