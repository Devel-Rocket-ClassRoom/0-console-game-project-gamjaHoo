using System.Collections.Generic;

namespace Framework.Engine
{
    public abstract class Scene
    {
        private readonly List<GameObject> _gameObjects = new List<GameObject>();
        private readonly List<GameObject> _pendingAdd = new List<GameObject>();
        private readonly List<GameObject> _pendingRemove = new List<GameObject>();
        private bool _isUpdating;

        public abstract void Load();
        public abstract void Update(float deltaTime);
        public abstract void Draw(ScreenBuffer buffer);
        public abstract void Unload();

        public void AddGameObject(GameObject gameObject)
        {
            if (_isUpdating)
            {
                _pendingAdd.Add(gameObject);
            }
            else
            {
                _gameObjects.Add(gameObject);
            }
        }

        public void RemoveGameObject(GameObject gameObject)
        {
            if (_isUpdating)
            {
                _pendingRemove.Add(gameObject);
            }
            else
            {
                _gameObjects.Remove(gameObject);
            }
        }

        public void ClearGameObjects()
        {
            _gameObjects.Clear();
            _pendingAdd.Clear();
            _pendingRemove.Clear();
        }

        protected void UpdateGameObjects(float deltaTime)
        {
            FlushPending();
            _isUpdating = true;

            for (int i = 0; i < _gameObjects.Count; i++)
            {
                if (_gameObjects[i].IsActive)
                {
                    _gameObjects[i].Update(deltaTime);
                }
            }

            _isUpdating = false;
        }

        protected void DrawGameObjects(ScreenBuffer buffer)
        {
            for (int i = 0; i < _gameObjects.Count; i++)
            {
                if (_gameObjects[i].IsActive)
                {
                    _gameObjects[i].Draw(buffer);
                }
            }
        }

        public GameObject FindGameObject(string name)
        {
            for (int i = 0; i < _gameObjects.Count; i++)
            {
                if (_gameObjects[i].Name == name)
                {
                    return _gameObjects[i];
                }
            }

            for (int i = 0; i < _pendingAdd.Count; i++)
            {
                if (_pendingAdd[i].Name == name)
                {
                    return _pendingAdd[i];
                }
            }

            return null;
        }

        private void FlushPending()
        {
            if (_pendingRemove.Count > 0)
            {
                for (int i = 0; i < _pendingRemove.Count; i++)
                {
                    _gameObjects.Remove(_pendingRemove[i]);
                }
                _pendingRemove.Clear();
            }

            if (_pendingAdd.Count > 0)
            {
                _gameObjects.AddRange(_pendingAdd);
                _pendingAdd.Clear();
            }
        }
    }
}
