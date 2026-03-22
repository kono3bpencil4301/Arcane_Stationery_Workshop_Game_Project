extends CharacterBody2D

@export var speed: float = 40.0
@export var player_node: Player
@export var wake_distance: float = 660
@export var sleep_distance: float = 700

@onready var stats: Stats = $Stats
@onready var hurtbox: HurtBox = $HurtBox
@onready var navigation_agent_2d: NavigationAgent2D = $Navigation/NavigationAgent2D
@onready var animation_player: AnimationPlayer = $AnimationPlayer
@onready var animator_flip = $AnimatedSprite2D

enum State {
	IDLE,
	SEARCH,
	HURT,
	DYING,
	DEAD
}

var current_state: State = State.SEARCH
var pending_damage: Damage = null

func _ready() -> void:
	$Navigation/Timer.timeout.connect(_on_timer_timeout)
	$Navigation/Timer.start()

	if not player_node:
		player_node = get_tree().get_first_node_in_group("player") as Player

	add_to_group("enemies")

	hurtbox.hurt.connect(_on_hurtbox_hurt)
	stats.no_health.connect(die)
	animation_player.animation_finished.connect(_on_animation_finished)

	transition_state(-1, current_state)

	# 不要太早查路
	call_deferred("_setup_navigation")


func _setup_navigation() -> void:
	if player_node:
		navigation_agent_2d.target_position = player_node.global_position
		print("初始导航目标:", player_node.global_position)


func _physics_process(_delta: float) -> void:
	var next_state := get_next_state(current_state)
	if next_state != current_state:
		transition_state(current_state, next_state)
		current_state = next_state

	match current_state:
		State.IDLE:
			idle_update()
		State.SEARCH:
			search_update()
		State.HURT:
			hurt_update()
		State.DYING:
			dying_update()

	update_facing()


func search_update() -> void:
	if player_node == null:
		velocity = Vector2.ZERO
		return

	if navigation_agent_2d.is_navigation_finished():
		velocity = Vector2.ZERO
		return

	var next_pos := navigation_agent_2d.get_next_path_position()

	if next_pos.is_equal_approx(global_position):
		velocity = Vector2.ZERO
		return

	var direction := global_position.direction_to(next_pos)
	velocity = direction * speed
	move_and_slide()


func hurt_update() -> void:
	search_update()


func dying_update() -> void:
	velocity = Vector2.ZERO
	move_and_slide()


func _on_timer_timeout() -> void:
	if player_node == null:
		return

	var dist_sq := get_player_distance_sq()
	var wake_sq := wake_distance * wake_distance

	if dist_sq <= wake_sq and navigation_agent_2d:
		navigation_agent_2d.target_position = player_node.global_position


	# 调试：看看目标是不是可达
	await get_tree().physics_frame
	if not navigation_agent_2d.is_target_reachable():
		print("目标当前不可达，最终可达点：", navigation_agent_2d.get_final_position())


func _on_hurtbox_hurt(hitbox: HitBox) -> void:
	print("hurt signal received from:", hitbox.name)
	stats.health -= 10
	print("敌人受击，当前血量:", stats.health)
	animation_player.play("Hurt")


func die() -> void:
	print("敌人死亡")


func get_next_state(state: State) -> State:
	if stats.health <= 0:
		return State.DYING

	var dist_sq := get_player_distance_sq()
	var wake_sq := wake_distance * wake_distance
	var sleep_sq := sleep_distance * sleep_distance

	match state:
		State.IDLE:
			if dist_sq <= wake_sq:
				return State.SEARCH
			return State.IDLE

		State.SEARCH:
			if dist_sq >= sleep_sq:
				return State.IDLE
			if pending_damage != null:
				return State.HURT
			return State.SEARCH

		State.HURT:
			if dist_sq >= sleep_sq:
				pending_damage = null
				return State.IDLE
			pending_damage = null
			return State.SEARCH

		State.DYING:
			return State.DYING

	return State.IDLE


func transition_state(from: int, to: State) -> void:
	print("[%s] %s -> %s" % [
		Engine.get_physics_frames(),
		"None" if from == -1 else State.keys()[from],
		State.keys()[to]
	])

	match to:
		State.IDLE:
			velocity = Vector2.ZERO
			animation_player.stop()

		State.SEARCH:
			animation_player.play("Search")

		State.HURT:
			if pending_damage:
				stats.health -= pending_damage.amount
				print("敌人受击，当前血量:", stats.health)
			animation_player.play("Hurt")

		State.DYING:
			animation_player.play("Die")
			await animation_player.animation_finished
			queue_free()


func _on_animation_finished(anim_name: StringName) -> void:
	if anim_name == "Hurt" and stats.health > 0:
		animation_player.play("Search")


func get_enemy_side() -> String:
	if player_node == null:
		return "center"

	if global_position.x < player_node.global_position.x:
		return "right"
	elif global_position.x > player_node.global_position.x:
		return "left"

	return "center"


func update_facing() -> void:
	var side := get_enemy_side()

	if side == "left":
		animator_flip.flip_h = true
	elif side == "right":
		animator_flip.flip_h = false
		
func get_player_distance_sq() -> float:
	if player_node == null:
		return INF
	return global_position.distance_squared_to(player_node.global_position)

func idle_update() -> void:
	velocity = Vector2.ZERO
	move_and_slide()
	
