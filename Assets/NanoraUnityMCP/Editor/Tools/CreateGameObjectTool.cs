using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using NanoraUnityMCP.Core;
using NanoraUnityMCP.Protocol;

namespace NanoraUnityMCP.Tools
{
    [McpTool("create_game_object")]
    public class CreateGameObjectTool : IMcpTool
    {
        public string Name => "create_game_object";
        public string Description => "Creates a new GameObject in the active scene, either empty or as a primitive (Cube, Sphere, etc.), " +
            "with local position/rotation/scale and an optional parent. The creation is undoable and the new object becomes the selection. " +
            "parent_path is resolved among active GameObjects; if it is not found the object is created at the scene root without an error.";

        public JObject InputSchema => JObject.Parse(@"{
            ""type"": ""object"",
            ""properties"": {
                ""name"": {
                    ""type"": ""string"",
                    ""description"": ""Name for the new GameObject"",
                    ""default"": ""New GameObject""
                },
                ""primitive_type"": {
                    ""type"": ""string"",
                    ""description"": ""Type of primitive to create (Cube, Sphere, Capsule, Cylinder, Plane, Quad, Empty)"",
                    ""enum"": [""Cube"", ""Sphere"", ""Capsule"", ""Cylinder"", ""Plane"", ""Quad"", ""Empty""],
                    ""default"": ""Empty""
                },
                ""parent_path"": {
                    ""type"": ""string"",
                    ""description"": ""Path to parent GameObject (optional)""
                },
                ""position"": {
                    ""type"": ""object"",
                    ""properties"": {
                        ""x"": { ""type"": ""number"", ""default"": 0 },
                        ""y"": { ""type"": ""number"", ""default"": 0 },
                        ""z"": { ""type"": ""number"", ""default"": 0 }
                    },
                    ""description"": ""Initial position""
                },
                ""rotation"": {
                    ""type"": ""object"",
                    ""properties"": {
                        ""x"": { ""type"": ""number"", ""default"": 0 },
                        ""y"": { ""type"": ""number"", ""default"": 0 },
                        ""z"": { ""type"": ""number"", ""default"": 0 }
                    },
                    ""description"": ""Initial rotation in euler angles""
                },
                ""scale"": {
                    ""type"": ""object"",
                    ""properties"": {
                        ""x"": { ""type"": ""number"", ""default"": 1 },
                        ""y"": { ""type"": ""number"", ""default"": 1 },
                        ""z"": { ""type"": ""number"", ""default"": 1 }
                    },
                    ""description"": ""Initial scale""
                }
            },
            ""required"": [""name""]
        }");

        public Task<McpToolResult> ExecuteAsync(JObject arguments)
        {
            return MainThreadDispatcher.RunAsync(() =>
            {
                string name = arguments?["name"]?.Value<string>() ?? "New GameObject";
                string primitiveType = arguments?["primitive_type"]?.Value<string>() ?? "Empty";
                string parentPath = arguments?["parent_path"]?.Value<string>();

                Vector3 position = ParseVector3(arguments?["position"] as JObject, Vector3.zero);
                Vector3 rotation = ParseVector3(arguments?["rotation"] as JObject, Vector3.zero);
                Vector3 scale = ParseVector3(arguments?["scale"] as JObject, Vector3.one);

                // Create the GameObject
                GameObject newObj;

                if (primitiveType.ToLower() == "empty" || string.IsNullOrEmpty(primitiveType))
                {
                    newObj = new GameObject(name);
                }
                else
                {
                    PrimitiveType primType = PrimitiveType.Cube;
                    switch (primitiveType.ToLower())
                    {
                        case "sphere": primType = PrimitiveType.Sphere; break;
                        case "capsule": primType = PrimitiveType.Capsule; break;
                        case "cylinder": primType = PrimitiveType.Cylinder; break;
                        case "plane": primType = PrimitiveType.Plane; break;
                        case "quad": primType = PrimitiveType.Quad; break;
                        default: primType = PrimitiveType.Cube; break;
                    }
                    newObj = GameObject.CreatePrimitive(primType);
                    newObj.name = name;
                }

                // Set parent if specified
                if (!string.IsNullOrEmpty(parentPath))
                {
                    var parent = GameObject.Find(parentPath);
                    if (parent != null)
                    {
                        newObj.transform.SetParent(parent.transform, false);
                    }
                }

                // Set transform
                newObj.transform.localPosition = position;
                newObj.transform.localEulerAngles = rotation;
                newObj.transform.localScale = scale;

                // Register undo
                Undo.RegisterCreatedObjectUndo(newObj, $"Create {name}");

                // Select the new object
                Selection.activeGameObject = newObj;

                return McpToolResult.Success($"Created GameObject '{name}' at position {position}");
            });
        }

        private Vector3 ParseVector3(JObject obj, Vector3 defaultValue)
        {
            if (obj == null) return defaultValue;

            float x = obj["x"]?.Value<float>() ?? defaultValue.x;
            float y = obj["y"]?.Value<float>() ?? defaultValue.y;
            float z = obj["z"]?.Value<float>() ?? defaultValue.z;

            return new Vector3(x, y, z);
        }
    }
}
