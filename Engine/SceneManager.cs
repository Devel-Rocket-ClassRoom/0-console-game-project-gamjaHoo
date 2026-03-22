namespace Framework.Engine
{
    public class SceneManager<TScene> where TScene : Scene
    {
        private TScene _currentScene;

        public event GameAction<TScene> SceneChanged;

        public TScene CurrentScene => _currentScene;

        public void ChangeScene(TScene scene)
        {
            if (_currentScene != null)
            {
                _currentScene.Unload();
            }
            _currentScene = scene;
            SceneChanged?.Invoke(_currentScene);
            _currentScene.Load();
        }
    }
}
