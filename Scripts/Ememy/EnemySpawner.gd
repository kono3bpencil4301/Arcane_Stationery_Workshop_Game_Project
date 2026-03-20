class_name EnemySpawner
extends Node2D

@export var spawn_points: Array[Marker2D]
@export var player: Node2D
@export var min_spawn_distance := 220.0
@export var max_alive_enemies := 40

var alive_enemies: Array[Node] = []

func can_spawn() -> bool:
    alive_enemies = alive_enemies.filter(func(e): return is_instance_valid(e))
    return alive_enemies.size() < max_alive_enemies

func get_valid_spawn_point() -> Marker2D:
    var candidates: Array[Marker2D] = []
    for p in spawn_points:
        if p.global_position.distance_to(player.global_position) >= min_spawn_distance:
            candidates.append(p)
    if candidates.is_empty():
        return null
    return candidates[randi() % candidates.size()]

func spawn_enemy(enemy_scene: PackedScene) -> Node:
    if not can_spawn():
        return null
    var point := get_valid_spawn_point()
    if point == null:
        return null

    var enemy = enemy_scene.instantiate()
    enemy.global_position = point.global_position
    get_tree().current_scene.add_child(enemy)
    alive_enemies.append(enemy)
    return enemy