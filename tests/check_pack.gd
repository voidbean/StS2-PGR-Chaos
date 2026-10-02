extends SceneTree

func _initialize():
    var args = OS.get_cmdline_user_args()
    if args.size() != 1 or not ProjectSettings.load_resource_pack(args[0]):
        push_error("Cannot mount prototype pack")
        quit(1)
        return
    for key in ["red", "yellow", "blue", "super", "ultimate", "charge", "core"]:
        var texture = ResourceLoader.load("res://ChaosPrototype/" + key + ".svg")
        if not texture is Texture2D or texture.get_width() != 500 or texture.get_height() != 380:
            push_error("Missing or invalid placeholder texture: " + key)
            quit(1)
            return
    print("PASS: all 7 placeholder textures loaded from exported PCK in isolated project")
    quit(0)
