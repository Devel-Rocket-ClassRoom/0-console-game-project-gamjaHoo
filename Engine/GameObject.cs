using System;

namespace Framework.Engine
{
    public abstract class GameObject
    {
        public string Name { get; set; } = "";
        public bool IsActive { get; set; } = true;
        public Scene Scene { get; }

        protected GameObject(Scene scene)
        {
            Scene = scene;
        }

        public abstract void Update(float deltaTime);
        public abstract void Draw(ScreenBuffer buffer);
    }
}
