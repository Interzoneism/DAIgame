@tool
extends Sprite2D

# This is the "Master Value" you change in the Inspector.
# The script will force the Shader and the Scale to match this.
@export var target_pixel_width: float = 48.0:
	set(value):
		target_pixel_width = value
		_update_visuals()

func _ready():
	# CRITICAL FIX:
	# Check if the material is shared. If so, duplicate it so this 
	# weapon has its own unique copy of the shader parameters.
	if material and not material.resource_local_to_scene:
		material = material.duplicate()
	
	_update_visuals()
	
	# Connect texture changes to update scale automatically
	if not texture_changed.is_connected(_update_visuals):
		texture_changed.connect(_update_visuals)

func _update_visuals():
	if not texture:
		return

	# 1. Update the Scale (Make the sprite the right size on screen)
	var texture_w = float(texture.get_width())
	var new_scale = target_pixel_width / texture_w
	scale = Vector2(new_scale, new_scale)
	
	# 2. Update the Shader (Make the pixelation grid match the new size)
	# This overwrites the "48.0" default in the shader file.
	if material is ShaderMaterial:
		material.set_shader_parameter("target_width", target_pixel_width)
