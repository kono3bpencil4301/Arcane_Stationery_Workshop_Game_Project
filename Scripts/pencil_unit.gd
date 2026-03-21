extends Node2D


enum State {
	IDLE,
	ATTACK
}

@onready var pivot: Node2D = $Pencil/pivot
@onready var anim: AnimationPlayer = $Pencil/AnimationPlayer
@export var attack_power: float = 10
@export var crit_chance: float = 0.2
@export var crit_multiplier: float = 1.5
@onready var attack_hitbox: Area2D = $Pencil
 

var state: State = State.IDLE
var has_hit_in_this_attack: bool = false
var attack_duration: float = 0.35
var idle_duration: float = 0.65
var cycle_duration: float = 1.0
var phase_offset: float = 0.0

func _ready() -> void:
	reset_to_idle_pose()
	print(name, " -> READY")

func setup_cycle(a_duration: float, i_duration: float, p_offset: float) -> void:
	attack_duration = a_duration
	idle_duration = i_duration
	cycle_duration = attack_duration + idle_duration
	phase_offset = p_offset
	reset_to_idle_pose()
	print(name, " -> setup_cycle")

func update_cycle(global_time: float) -> void:
	var local_time := fmod(global_time + phase_offset, cycle_duration)

	if local_time < attack_duration:
		if state != State.ATTACK:
			enter_attack()
	else:
		if state != State.IDLE:
			enter_idle()

func reset_to_idle_pose() -> void:
	state = State.IDLE
	pivot.position = Vector2.ZERO
	anim.stop()

func enter_attack() -> void:
	if state == State.ATTACK:
		return
	state = State.ATTACK
	has_hit_in_this_attack = false
	anim.play("Attack_Out")

func enter_idle() -> void:
	if state == State.IDLE:
		return
	state = State.IDLE
	anim.play("Attack_Back")
	

func roll_damage() -> Dictionary:
	var is_crit := randf() < crit_chance
	var final_damage := attack_power

	if is_crit:
		final_damage = int(round(attack_power * crit_multiplier))

	return {
		"damage": final_damage,
		"is_crit": is_crit
	}


func deal_damage_to(target: Node) -> void:
	if has_hit_in_this_attack:
		return
	if target == null:
		return
	if not target.has_method("take_damage"):
		return

	var result := roll_damage()
	target.take_damage(result["damage"], result["is_crit"])
	has_hit_in_this_attack = true
	
func _on_attack_hitbox_body_entered(body: Node) -> void:
	if state == State.ATTACK:
		deal_damage_to(body)
		print("命中敌人：", body.name, "，触发伤害")
