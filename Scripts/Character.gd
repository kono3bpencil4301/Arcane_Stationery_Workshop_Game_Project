class_name Player
extends CharacterBody2D

@export var move_speed: float
@export var animator: AnimatedSprite2D
@export var unit_name: String = "Player"

var last_dir := "idle_f"  # 记录当前朝向

func _ready() -> void:
	# 添加到玩家组
	add_to_group("player")

	# 如果没有手动指定 animator，自动获取子节点中的 AnimatedSprite2D
	if animator == null:
		animator = $Character as AnimatedSprite2D

func _physics_process(_delta: float) -> void:
	var input_dir := Input.get_vector("left", "right", "up", "down")

	velocity = input_dir * move_speed
	move_and_slide()

	if input_dir != Vector2.ZERO:
		# 根据移动方向改变朝向
		if input_dir.y > 0:
			last_dir = "move_f"
			scale.x = 1
		elif input_dir.y < 0:
			last_dir = "move_b"
			scale.x = 1
		elif input_dir.x > 0:
			last_dir = "move_r"
			scale.x = 1
		elif input_dir.x < 0:
			last_dir = "move_l"
			scale.x = 1

		if animator != null:
			animator.play(last_dir)
	elif animator != null:
		var idle_anim := last_dir.replace("move_", "idle_")
		animator.play(idle_anim)
