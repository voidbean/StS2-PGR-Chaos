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
    for skin in ["normal", "fusion"]:
        for key in ["standing", "portrait"]:
            var texture = load("res://ChaosPrototype/character/" + skin + "/" + key + ".png") as Texture2D
            if texture == null or texture.get_width() < 100 or not texture.get_image().detect_alpha():
                push_error("Missing character image or transparency: " + key)
                quit(1)
                return
        for key in ["combat", "select", "icon", "rest", "merchant"]:
            var scene = load("res://ChaosPrototype/character/" + skin + "/" + key + ".tscn") as PackedScene
            if scene == null:
                push_error("Missing character scene: " + key)
                quit(1)
                return
            var node = scene.instantiate()
            if key == "combat":
                for required in ["Visuals", "PhobiaModeVisuals", "Bounds", "CenterPos", "IntentPos", "OrbPos", "TalkPos", "FormVfx"]:
                    if not node.has_node("%" + required):
                        push_error("Missing combat anchor: " + required)
                        quit(1)
                        return
                if node.get_node("Visuals").texture != node.get_node("PhobiaModeVisuals").texture:
                    push_error("Phobia mode must retain the character artwork")
                    quit(1)
                    return
            if key in ["select", "icon"] and not node is Control:
                push_error("UI scene requires a Control root: " + key)
                quit(1)
                return
            node.free()
    print("PASS: 7 card textures, 4 transparent character images and 10 character scenes loaded from isolated PCK")
    quit(0)
