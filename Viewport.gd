extends Viewport


# Declare member variables here. Examples:
# var a = 2
# var b = "text"


# Called when the node enters the scene tree for the first time.
func _ready():
	return;
	get_parent().rect_size = Vector2(800, 600)
	size = Vector2(800, 600)
	print(get_parent().rect_size)
	print(size)


# Called every frame. 'delta' is the elapsed time since the previous frame.
#func _process(delta):
#	pass
