extends CharacterBody2D

@export var speed: float = 40.0
@export var player_node: Player

@onready var stats: Stats = $Stats
@onready var hurtbox: HurtBox = $HurtBox
@onready var navigation_agent_2d: NavigationAgent2D = $Navigation/NavigationAgent2D
@onready var animation_player: AnimationPlayer = $AnimationPlayer

enum State {
	SEARCH,
	HURT,
	DYING
}

var current_state: State = State.SEARCH
var pending_damage: Damage = null

func _ready() -> void:
	$Navigation/Timer.timeout.connect(_on_timer_timeout)
	$Navigation/Timer.start()

	await get_tree().process_frame

	if not player_node:
		player_node = get_tree().get_first_node_in_group("player") as Player

	if player_node:
		navigation_agent_2d.target_position = player_node.global_position
		print("导航目标已设置: ", player_node.global_position)

	add_to_group("enemies")

	hurtbox.hurt.connect(_on_hurtbox_hurt)
	stats.no_health.connect(die)

	transition_state(-1, current_state)
	animation_player.animation_finished.connect(_on_animation_finished)

func _physics_process(_delta: float) -> void:
	var next_state := get_next_state(current_state)
	if next_state != current_state:
		transition_state(current_state, next_state)
		current_state = next_state

	match current_state:
		State.SEARCH:
			search_update()
		State.HURT:
			hurt_update()
		State.DYING:
			dying_update()

func search_update() -> void:
	if navigation_agent_2d.target_position == Vector2.INF:
		return
	if navigation_agent_2d.is_navigation_finished():
		return

	var next_pos := navigation_agent_2d.get_next_path_position()
	if next_pos == Vector2.ZERO:
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
	if player_node and navigation_agent_2d:
		navigation_agent_2d.target_position = player_node.global_position

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

	match state:
		State.SEARCH:
			if pending_damage != null:
				return State.HURT
			return State.SEARCH

		State.HURT:
			pending_damage = null
			return State.SEARCH

		State.DYING:
			return State.DYING

	return State.SEARCH

func transition_state(from: int, to: State) -> void:
	print("[%s] %s -> %s" % [
		Engine.get_physics_frames(),
		"None" if from == -1 else State.keys()[from],
		State.keys()[to]
	])

	match to:
		State.SEARCH:
			animation_player.play("Search")

		State.HURT:
			if pending_damage:
				stats.health -= pending_damage.amount
				print("敌人受击，当前血量:", stats.health)
			animation_player.play("Hurt")

		State.DYING:
			animation_player.play("Die")
			
func _on_animation_finished(anim_name: StringName) -> void:
	if anim_name == "Hurt" and stats.health > 0:
		animation_player.play("Search")
