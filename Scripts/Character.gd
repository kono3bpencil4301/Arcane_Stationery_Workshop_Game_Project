class_name Player
extends CharacterBody2D

@export var move_speed: float = 100.0
@export var animator: AnimatedSprite2D
@export var unit_name: String = "Player"

@onready var animation_player: AnimationPlayer = $AnimationPlayer
@onready var stats: Stats = $Stats
@onready var hurt_box: HurtBox = $HurtBox

var last_dir := "idle_f"
var pending_damage: int = 0

enum State {
	IDLE,
	MOVE,
	HURT,
	DYING
}

var current_state: State = State.IDLE


func _ready() -> void:
	add_to_group("player")

	if animator == null:
		animator = $Character as AnimatedSprite2D

	if not hurt_box.hurt.is_connected(_on_hurt_box_hurt):
		hurt_box.hurt.connect(_on_hurt_box_hurt)

	if not animation_player.animation_finished.is_connected(_on_animation_player_animation_finished):
		animation_player.animation_finished.connect(_on_animation_player_animation_finished)

	print("玩家初始血量:", stats.health)
	_enter_state(current_state)


func _physics_process(_delta: float) -> void:
	match current_state:
		State.IDLE:
			state_idle()
		State.MOVE:
			state_move()
		State.HURT:
			state_hurt()
		State.DYING:
			state_dying()


func change_state(new_state: State) -> void:
	if new_state == current_state:
		return

	print("[%s] %s -> %s" % [
		Engine.get_physics_frames(),
		State.keys()[current_state],
		State.keys()[new_state]
	])

	current_state = new_state
	_enter_state(new_state)


func _enter_state(state: State) -> void:
	match state:
		State.IDLE:
			velocity = Vector2.ZERO
			if animator != null:
				animator.play(last_dir.replace("move_", "idle_"))

		State.MOVE:
			pass

		State.HURT:
			print("[Player] enter HURT")
			print("[Player] stats =", stats)
			print("[Player] before =", stats.health)
			print("[Player] pending_damage =", pending_damage)

			var damage := pending_damage
			pending_damage = 0

			stats.health -= damage

			print("[Player] after =", stats.health)

			if stats.health <= 0:
				change_state(State.DYING)
				return

			animation_player.play("Hurt")

		State.DYING:
			velocity = Vector2.ZERO
			print("进入 DYING，当前血量:", stats.health)
			animation_player.play("Die")


func state_idle() -> void:
	var input_dir := Input.get_vector("left", "right", "up", "down")

	velocity = Vector2.ZERO
	move_and_slide()

	if input_dir != Vector2.ZERO:
		change_state(State.MOVE)


func state_move() -> void:
	var input_dir := Input.get_vector("left", "right", "up", "down")

	if input_dir == Vector2.ZERO:
		change_state(State.IDLE)
		return

	velocity = input_dir * move_speed
	move_and_slide()

	if input_dir.y > 0:
		last_dir = "move_f"
	elif input_dir.y < 0:
		last_dir = "move_b"
	elif input_dir.x > 0:
		last_dir = "move_r"
	elif input_dir.x < 0:
		last_dir = "move_l"

	if animator != null:
		animator.play(last_dir)


func state_hurt() -> void:
	velocity = Vector2.ZERO
	move_and_slide()


func state_dying() -> void:
	velocity = Vector2.ZERO
	move_and_slide()


func _on_hurt_box_hurt(hitbox: HitBox) -> void:
	print("收到 hurt 信号，来源:", hitbox)

	if current_state == State.DYING:
		return

	if current_state == State.HURT:
		return

	pending_damage = hitbox.damage
	print("记录伤害后 pending_damage =", pending_damage)
	change_state(State.HURT)


func _on_animation_player_animation_finished(anim_name: StringName) -> void:
	print("动画播放完毕:", anim_name, " 当前状态:", State.keys()[current_state])

	if anim_name == "Hurt" and current_state == State.HURT:
		var input_dir := Input.get_vector("left", "right", "up", "down")
		if input_dir == Vector2.ZERO:
			change_state(State.IDLE)
		else:
			change_state(State.MOVE)

	elif anim_name == "Die" and current_state == State.DYING:
		queue_free()
		
func die() ->void:
	get_tree().reload_current_scene()
