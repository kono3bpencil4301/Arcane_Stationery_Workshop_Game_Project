extends Node2D

@onready var nav_region: NavigationRegion2D = $NavigationRegion2D

func _ready() -> void:
	# 等待场景完全加载
	await get_tree().process_frame
	await get_tree().process_frame

	# 烘焙导航
	nav_region.bake_navigation_polygon()
	print("导航已烘焙")
