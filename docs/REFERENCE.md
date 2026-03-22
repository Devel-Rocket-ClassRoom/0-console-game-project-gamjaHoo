# Framework 학생용 레퍼런스

## 목차

1. [프로젝트 구성](#1-프로젝트-구성)
2. [아키텍처](#2-아키텍처)
3. [Engine API](#3-engine-api)
4. [게임 만들기](#4-게임-만들기)
5. [패턴 & 테크닉](#5-패턴--테크닉)
6. [자주 하는 실수](#6-자주-하는-실수)
7. [빠른 참조](#7-빠른-참조)

---

## 1. 프로젝트 구성

콘솔 기반 2D 게임을 만들 수 있는 C# 교육용 프레임워크입니다. `Engine` 폴더가 공통 기능을 제공하고, `GameApp`을 상속하여 자신만의 게임을 만듭니다.

| 항목 | 값 |
|------|-----|
| 대상 프레임워크 | .NET 10.0 |
| C# 버전 | 9.0 |

### 폴더 구조

```
Framework/
├── Framework.csproj
├── Program.cs                # 진입점
│
├── Engine/                   # 프레임워크 엔진 (수정하지 않음)
│   ├── GameApp.cs
│   ├── GameObject.cs
│   ├── Scene.cs
│   ├── SceneManager.cs
│   ├── ScreenBuffer.cs
│   ├── Input.cs
│   └── GameEvent.cs
│
└── MyGame/                   # 여러분의 게임 (직접 만듦)
    ├── MyGameApp.cs
    ├── TitleScene.cs
    └── PlayScene.cs
```

---

## 2. 아키텍처

### Engine + 게임 코드

```
┌─────────────────────────────────────────────┐
│                 여러분의 게임                  │
│  (GameApp 서브클래스 + Scene들 + 게임 로직)     │
├─────────────────────────────────────────────┤
│               Engine (프레임워크)              │
│  GameApp │ Scene │ ScreenBuffer │ Input │ …  │
└─────────────────────────────────────────────┘
```

- **Engine**: 게임 루프, 렌더링, 입력 처리. 수정하지 않습니다.
- **게임 코드**: `GameApp`을 상속하고 `Scene`을 구현합니다.

### 게임 루프

`GameApp.Run()` 안에서 매 프레임 실행됩니다.

```
┌──────────────────────────────────────────┐
│              게임 루프 (1프레임)            │
│                                          │
│  1. Input.Poll()    ← 키보드 입력 읽기     │
│  2. Update(dt)      ← 게임 로직 처리      │
│  3. Buffer.Clear()  ← 화면 버퍼 초기화     │
│  4. Draw()          ← 화면 그리기          │
│  5. Buffer.Present()← 전체 프레임 출력     │
│                                          │
│  약 33ms 간격 → ~30 FPS                   │
└──────────────────────────────────────────┘
```

### 클래스 관계도

```
                    GameAction (delegate)
                    GameAction<T> (delegate)

    GameApp (abstract)
       △
       │
    MyGame (여러분의 GameApp 서브클래스)
       │
       ▼
  SceneManager<TScene>
       │
       ▼
    Scene (abstract)
       △
    ┌──┴──────────┬─────────────┐
  TitleScene   PlayScene   ... (여러분의 씬들)
       │
       ▼ (AddGameObject로 관리)
  GameObject (abstract)
       △
    ┌──┴──────┬─────────┐
  Player    Enemy    ... (여러분의 게임 오브젝트들)

  ScreenBuffer ← GameApp이 소유, Scene에 전달
  Input        ← 정적 클래스, 어디서든 접근
```

---

## 3. Engine API

### 3.1 GameApp (추상 클래스)

모든 게임이 상속하는 뼈대입니다.

```csharp
namespace Framework.Engine
{
    public abstract class GameApp
    {
        protected GameApp(int width, int height);

        protected ScreenBuffer Buffer { get; }

        public event GameAction GameStarted;
        public event GameAction GameStopped;

        public void Run();
        protected void Quit();

        protected abstract void Initialize();
        protected abstract void Update(float deltaTime);
        protected abstract void Draw();
    }
}
```

| 메서드 | 호출 시점 | 역할 |
|--------|----------|------|
| `Initialize()` | `Run()` 직후 1회 | SceneManager 생성, 첫 씬 설정 |
| `Update(deltaTime)` | 매 프레임 | 입력 처리, 게임 로직, 씬 위임 |
| `Draw()` | 매 프레임 (Update 후) | 현재 씬의 Draw 호출 |
| `Quit()` | 아무 때나 | 게임 루프 종료 |

**deltaTime**: 이전 프레임에서 현재 프레임까지 경과한 시간(초 단위 `float`). 약 `0.033`초.

### 3.2 Scene (추상 클래스)

게임의 한 화면(타이틀, 플레이, 결과 등)을 나타냅니다.

```csharp
namespace Framework.Engine
{
    public abstract class Scene
    {
        // 생명주기
        public abstract void Load();
        public abstract void Update(float deltaTime);
        public abstract void Draw(ScreenBuffer buffer);
        public abstract void Unload();

        // GameObject 관리 — public (Scene 외부에서도 호출 가능)
        public void AddGameObject(GameObject gameObject);
        public void RemoveGameObject(GameObject gameObject);
        public void ClearGameObjects();
        public GameObject FindGameObject(string name);

        // GameObject 일괄 처리 — protected (Scene 서브클래스 전용)
        protected void UpdateGameObjects(float deltaTime);
        protected void DrawGameObjects(ScreenBuffer buffer);
    }
}
```

**생명주기:**

```
ChangeScene(scene) 호출
    ↓
  Load()        ← 초기화 (변수, 보드, 상태 등)
    ↓
┌─ Update(dt) ← 입력 처리, 게임 로직
│  Draw(buf)  ← 화면 그리기
└─ (반복)
    ↓
  Unload()      ← 정리 (다음 씬으로 전환 시 자동 호출)
```

- `Load()`: 생성자 대신 여기서 초기화합니다.
- `Unload()`: 대부분 빈 구현으로 충분합니다.

**GameObject 관리 메서드:**

| 메서드 | 역할 |
|--------|------|
| `AddGameObject()` | 오브젝트 추가 (Update 순회 중이면 대기열에 보관, 아니면 즉시 반영) |
| `RemoveGameObject()` | 오브젝트 제거 (Update 순회 중이면 대기열에 보관) |
| `ClearGameObjects()` | 전체 오브젝트 + 대기열 일괄 제거 (스테이지 리셋용) |
| `UpdateGameObjects(dt)` | 대기열 반영 후 활성 오브젝트의 `Update()` 순회 호출 |
| `DrawGameObjects(buf)` | 활성 오브젝트의 `Draw()` 순회 호출 |
| `FindGameObject(name)` | 이름으로 오브젝트 탐색 (없으면 `null`) |

- `IsActive`가 `false`인 오브젝트는 `UpdateGameObjects`/`DrawGameObjects`에서 스킵됩니다.
- `Load()`에서 `AddGameObject()`를 호출하면 즉시 반영됩니다 (순회 중이 아니므로).

### 3.3 GameObject (추상 클래스)

게임 내 개별 오브젝트(플레이어, 적, 아이템 등)를 나타냅니다. `Scene`이 `AddGameObject()`로 관리합니다.

```csharp
namespace Framework.Engine
{
    public abstract class GameObject
    {
        public string Name { get; set; } = "";
        public bool IsActive { get; set; } = true;
        public Scene Scene { get; }

        protected GameObject(Scene scene);

        public abstract void Update(float deltaTime);
        public abstract void Draw(ScreenBuffer buffer);
    }
}
```

| 멤버 | 기본값 | 역할 |
|------|--------|------|
| `Name` | `""` | 이름으로 탐색 가능 (`FindGameObject`) |
| `IsActive` | `true` | `false`면 Update/Draw 모두 스킵 |
| `Scene` | (생성자 주입) | 소속된 Scene 참조. `Scene.AddGameObject()` 등 호출에 사용 |

- 생성자에 `Scene`을 전달해야 합니다. 서브클래스에서 `base(scene)`으로 호출합니다.
- `Scene` 프로퍼티를 통해 오브젝트 내부에서 다른 오브젝트를 생성/제거할 수 있습니다.

**사용 예:**

```csharp
public class Player : GameObject
{
    private int _x, _y;

    public Player(Scene scene, int x, int y) : base(scene)
    {
        _x = x;
        _y = y;
    }

    public override void Update(float deltaTime)
    {
        if (Input.IsKeyDown(ConsoleKey.RightArrow))
        {
            _x++;
        }

        // 스페이스바로 발사체 생성
        if (Input.IsKeyDown(ConsoleKey.Spacebar))
        {
            Scene.AddGameObject(new Bullet(Scene, _x, _y - 1));
        }
    }

    public override void Draw(ScreenBuffer buffer)
    {
        buffer.SetCell(_x, _y, '@', ConsoleColor.Yellow);
    }
}
```

### 3.4 SceneManager\<TScene\>

씬 전환을 관리합니다. 보통 `SceneManager<Scene>` 타입으로 사용합니다.

```csharp
namespace Framework.Engine
{
    public class SceneManager<TScene> where TScene : Scene
    {
        public TScene CurrentScene { get; }
        public event GameAction<TScene> SceneChanged;
        public void ChangeScene(TScene scene);
    }
}
```

**`ChangeScene()` 동작 순서:**

1. 현재 씬이 있으면 `Unload()` 호출
2. 새 씬을 현재 씬으로 설정
3. `SceneChanged` 이벤트 발행
4. 새 씬의 `Load()` 호출

### 3.5 ScreenBuffer

콘솔 화면에 그리기 위한 오프스크린 버퍼입니다.

```csharp
namespace Framework.Engine
{
    public class ScreenBuffer
    {
        public int Width { get; }
        public int Height { get; }

        // 기본 그리기
        public void SetCell(int x, int y, char ch,
            ConsoleColor color = ConsoleColor.Gray,
            ConsoleColor bgColor = ConsoleColor.Black);

        public void WriteText(int x, int y, string text,
            ConsoleColor color = ConsoleColor.Gray,
            ConsoleColor bgColor = ConsoleColor.Black);

        // 센터링 & 멀티라인
        public void WriteTextCentered(int y, string text,
            ConsoleColor color = ConsoleColor.Gray,
            ConsoleColor bgColor = ConsoleColor.Black);

        public void WriteLines(int x, int y, string[] lines,
            ConsoleColor color = ConsoleColor.Gray,
            ConsoleColor bgColor = ConsoleColor.Black);

        // 드로잉 프리미티브
        public void DrawHLine(int x, int y, int length, char ch = '-',
            ConsoleColor color = ConsoleColor.Gray,
            ConsoleColor bgColor = ConsoleColor.Black);

        public void DrawVLine(int x, int y, int length, char ch = '|',
            ConsoleColor color = ConsoleColor.Gray,
            ConsoleColor bgColor = ConsoleColor.Black);

        public void DrawBox(int x, int y, int width, int height,
            ConsoleColor color = ConsoleColor.Gray,
            ConsoleColor bgColor = ConsoleColor.Black);

        public void FillRect(int x, int y, int width, int height, char ch = ' ',
            ConsoleColor color = ConsoleColor.Gray,
            ConsoleColor bgColor = ConsoleColor.Black);

        public void Clear();
        public void Present();
    }
}
```

**좌표계:**

```
(0,0) ────────────────→ x (Width-1, 0)
  │
  │    화면 영역
  │
  ▼
(0, Height-1)         (Width-1, Height-1)
```

- `(0, 0)` = 좌상단. `x`는 가로(열), `y`는 세로(행).
- 범위 밖 좌표는 무시됩니다 (에러 없음).

**렌더링 원리:** 매 프레임마다 `Clear()` → `Draw()`에서 버퍼에 그림 → `Present()`에서 ANSI escape code를 포함한 단일 문자열로 조립하여 `Console.Write()` 한 번으로 출력. 깜빡임 없이 부드럽게 표시됩니다.

**헬퍼 메서드 사용 예:**

```csharp
// 타이틀을 화면 중앙에 자동 배치
buffer.WriteTextCentered(5, "MY GAME", ConsoleColor.Yellow);

// 정적 구조를 string 배열로 정의하고 한번에 그리기
string[] grid = new string[]
{
    "   |   |   ",
    "---+---+---",
    "   |   |   "
};
buffer.WriteLines(15, 7, grid, ConsoleColor.Gray);

// 구분선
buffer.DrawHLine(2, 6, 21, '-', ConsoleColor.DarkGray);

// 사각형 테두리
buffer.DrawBox(5, 3, 10, 5, ConsoleColor.White);

// 영역 채우기
buffer.FillRect(6, 4, 8, 3, '.', ConsoleColor.DarkGray);
```

### 3.6 Input (정적 클래스)

Windows `GetAsyncKeyState` 기반 키보드 입력을 처리합니다. `Poll()`은 엔진이 매 프레임 자동 호출합니다.

```csharp
namespace Framework.Engine
{
    public static class Input
    {
        public static bool HasInput { get; }

        public static void Poll();

        // 이번 프레임에 눌려있는지 (held)
        public static bool IsKey(ConsoleKey key);

        // 이전 프레임에 안 눌렸다가 이번 프레임에 눌린 순간 (edge-triggered)
        public static bool IsKeyDown(ConsoleKey key);

        // 이전 프레임에 눌렸다가 이번 프레임에 뗀 순간
        public static bool IsKeyUp(ConsoleKey key);
    }
}
```

| 메서드 | 감지 방식 | 용도 |
|--------|----------|------|
| `IsKey()` | 누르고 있는 동안 매 프레임 | 캐릭터 이동 등 연속 입력 |
| `IsKeyDown()` | 누른 순간 1회 | 메뉴 선택, 칸 놓기 등 1회성 입력 |
| `IsKeyUp()` | 뗀 순간 1회 | 키를 뗄 때 동작 |

**추적하는 키 목록:**

| 분류 | 키 |
|------|-----|
| 방향키 | `UpArrow`, `DownArrow`, `LeftArrow`, `RightArrow` |
| 숫자키 | `D0`~`D9`, `NumPad0`~`NumPad9` |
| 특수키 | `Enter`, `Escape`, `Spacebar`, `Tab`, `Backspace` |
| 영문자 | `H`, `S`, `Y`, `N`, `W`, `A`, `D` |

위 목록에 없는 키는 감지되지 않습니다. 필요하면 `Input.cs`의 `s_trackedKeys` 배열에 추가하세요.

**사용 예:**

```csharp
if (Input.IsKeyDown(ConsoleKey.Enter))
{
    // Enter를 누른 순간 1회 실행
}
```

### 3.7 GameAction 델리게이트

```csharp
namespace Framework.Engine
{
    public delegate void GameAction();
    public delegate void GameAction<T>(T value);
}
```

씬은 이벤트로 "무엇이 일어났다"를 알리고, GameApp이 반응합니다.

```csharp
// Scene: 이벤트 선언 + 발행
public event GameAction StartRequested;

if (Input.IsKeyDown(ConsoleKey.Enter))
{
    StartRequested?.Invoke();
}

// GameApp: 이벤트 구독 + 처리
title.StartRequested += () => ChangeToPlay();
```

---

## 4. 게임 만들기

### 4.1 GameApp 서브클래스

```csharp
using System;
using Framework.Engine;

namespace Framework.MyGame
{
    public class MyGame : GameApp
    {
        private readonly SceneManager<Scene> _scenes;

        public MyGame() : base(40, 20)
        {
            _scenes = new SceneManager<Scene>();
        }

        protected override void Initialize()
        {
            ChangeToTitle();
        }

        protected override void Update(float deltaTime)
        {
            if (Input.IsKeyDown(ConsoleKey.Escape))
            {
                Quit();
                return;
            }
            _scenes.CurrentScene?.Update(deltaTime);
        }

        protected override void Draw()
        {
            _scenes.CurrentScene?.Draw(Buffer);
        }

        private void ChangeToTitle()
        {
            TitleScene title = new TitleScene();
            title.StartRequested += () => ChangeToPlay();
            _scenes.ChangeScene(title);
        }

        private void ChangeToPlay()
        {
            PlayScene play = new PlayScene();
            play.PlayAgainRequested += () => ChangeToPlay();
            _scenes.ChangeScene(play);
        }
    }
}
```

- `base(40, 20)`: 화면 크기 40x20
- `_scenes`: 씬 관리자. `Update()`와 `Draw()`에서 현재 씬에 위임
- `ChangeToXxx()`: 씬 생성 → 이벤트 연결 → `ChangeScene()`

### 4.2 TitleScene

```csharp
using System;
using Framework.Engine;

namespace Framework.MyGame
{
    public class TitleScene : Scene
    {
        public event GameAction StartRequested;

        public override void Load()
        {
        }

        public override void Update(float deltaTime)
        {
            if (Input.IsKeyDown(ConsoleKey.Enter))
            {
                StartRequested?.Invoke();
            }
        }

        public override void Draw(ScreenBuffer buffer)
        {
            buffer.WriteTextCentered(5, "MY GAME", ConsoleColor.Yellow);
            buffer.WriteTextCentered(8, "Press ENTER to Start", ConsoleColor.Gray);
            buffer.WriteTextCentered(10, "Press ESC to Quit", ConsoleColor.DarkGray);
        }

        public override void Unload()
        {
        }
    }
}
```

### 4.3 PlayScene

```csharp
using System;
using Framework.Engine;

namespace Framework.MyGame
{
    public class PlayScene : Scene
    {
        private int _score;
        private bool _gameOver;

        public event GameAction PlayAgainRequested;

        public override void Load()
        {
            _score = 0;
            _gameOver = false;
        }

        public override void Update(float deltaTime)
        {
            if (_gameOver)
            {
                if (Input.IsKeyDown(ConsoleKey.Enter))
                {
                    PlayAgainRequested?.Invoke();
                }
                return;
            }

            // 게임 로직...
        }

        public override void Draw(ScreenBuffer buffer)
        {
            buffer.WriteText(5, 2, "Score: " + _score, ConsoleColor.White);

            if (_gameOver)
            {
                buffer.WriteText(10, 10, "Game Over! Press ENTER", ConsoleColor.Red);
            }
        }

        public override void Unload()
        {
        }
    }
}
```

**원칙: Update()는 로직, Draw()는 그림**

| 메서드 | 하는 일 | 하면 안 되는 일 |
|--------|---------|-----------------|
| `Load()` | 변수 초기화, 보드 생성 | - |
| `Update()` | 입력 처리, 상태 변경, 승리 체크 | 화면 출력 |
| `Draw()` | 현재 상태를 화면에 표시 | 상태 변경 |
| `Unload()` | 정리 | - |

### 4.4 GameObject 활용 PlayScene

`GameObject`를 사용하면 개별 오브젝트의 로직과 렌더링을 분리할 수 있습니다. 생성자에 `this`(Scene 자신)를 전달합니다.

```csharp
using System;
using Framework.Engine;

namespace Framework.MyGame
{
    public class PlayScene : Scene
    {
        private int _score;

        public event GameAction PlayAgainRequested;

        public override void Load()
        {
            _score = 0;
            AddGameObject(new Player(this, 10, 5) { Name = "player" });
            AddGameObject(new Enemy(this, 20, 8) { Name = "enemy" });
        }

        public override void Update(float deltaTime)
        {
            UpdateGameObjects(deltaTime);

            // 이름으로 오브젝트 탐색
            GameObject player = FindGameObject("player");
            if (player != null && !player.IsActive)
            {
                // 게임 오버 처리
            }
        }

        public override void Draw(ScreenBuffer buffer)
        {
            DrawGameObjects(buffer);
            buffer.WriteText(0, 0, "Score: " + _score, ConsoleColor.White);
        }

        public override void Unload()
        {
            ClearGameObjects();
        }
    }
}
```

- `Load()`에서 `AddGameObject()`로 오브젝트 등록 — 생성자에 `this` 전달
- `Update()`에서 `UpdateGameObjects()`로 일괄 업데이트
- `Draw()`에서 `DrawGameObjects()`로 일괄 렌더링 + 추가 UI 직접 그리기
- `Unload()`에서 `ClearGameObjects()`로 정리

**GameObject에서 다른 오브젝트 생성/제거:**

```csharp
public class Bullet : GameObject
{
    private int _x, _y;

    public Bullet(Scene scene, int x, int y) : base(scene)
    {
        _x = x;
        _y = y;
    }

    public override void Update(float deltaTime)
    {
        _y--;
        if (_y < 0)
        {
            Scene.RemoveGameObject(this);
        }
    }

    public override void Draw(ScreenBuffer buffer)
    {
        buffer.SetCell(_x, _y, '|', ConsoleColor.Yellow);
    }
}
```

- `Scene.AddGameObject()` / `Scene.RemoveGameObject()`는 `public`이므로 GameObject 내부에서 자유롭게 호출 가능
- Update 순회 중 호출하면 대기열에 보관되었다가 다음 프레임에 반영됩니다

### 4.5 씬 전환 연결

```csharp
private void ChangeToTitle()
{
    TitleScene title = new TitleScene();
    // 1. 먼저 이벤트 연결
    title.StartRequested += () => ChangeToPlay();
    // 2. 그 다음 씬 전환 (내부에서 Load() 호출됨)
    _scenes.ChangeScene(title);
}
```

순서: 씬 생성 → 이벤트 연결(`+=`) → `ChangeScene()`

### 4.6 Program.cs에서 실행

```csharp
new MyGame().Run();
```

---

## 5. 패턴 & 테크닉

### 5.1 씬 전환 패턴

```
TitleScene ──Enter──► PlayScene ──Enter──► PlayScene(새 인스턴스)
    △                                         │
    └──── ESC로 Quit() ◄──────────────────────┘
```

### 5.2 씬 내부 상태 머신

하나의 씬 안에서 여러 단계가 있을 때, `enum` + `switch`로 관리합니다.

```csharp
private enum PlayState { Betting, Dealing, PlayerTurn, DealerTurn, Result }

private PlayState _state;

public override void Update(float deltaTime)
{
    switch (_state)
    {
        case PlayState.Betting:
            UpdateBetting();
            break;
        case PlayState.Dealing:
            UpdateDealing(deltaTime);
            break;
        case PlayState.PlayerTurn:
            UpdatePlayerTurn();
            break;
        case PlayState.DealerTurn:
            UpdateDealerTurn(deltaTime);
            break;
        case PlayState.Result:
            UpdateResult();
            break;
    }
}
```

### 5.3 타이머 (deltaTime 누적)

```csharp
private float _timer;

// Update()에서
_timer += deltaTime;
if (_timer >= 0.4f)
{
    _timer = 0f;
    // 동작 수행
}
```

| 용도 | 간격 예시 |
|------|----------|
| 애니메이션 (카드 뒤집기 등) | 0.3~0.5초 |
| AI 턴 딜레이 | 0.5~1.0초 |
| 결과 표시 후 전환 | 1.0~2.0초 |

### 5.4 색상

`ConsoleColor` 열거형으로 16가지 색상을 사용할 수 있습니다.

| 색상 | 값 | 주 용도 |
|------|----|---------|
| `Black` | 0 | 배경 기본값 |
| `DarkBlue` | 1 | |
| `DarkGreen` | 2 | |
| `DarkCyan` | 3 | 조작 안내 |
| `DarkRed` | 4 | |
| `DarkMagenta` | 5 | |
| `DarkYellow` | 6 | 무승부 |
| `Gray` | 7 | 기본 전경색 |
| `DarkGray` | 8 | 비활성 텍스트 |
| `Blue` | 9 | |
| `Green` | 10 | 승리 |
| `Cyan` | 11 | 선택된 항목 |
| `Red` | 12 | 패배 |
| `Magenta` | 13 | |
| `Yellow` | 14 | 제목, 커서 |
| `White` | 15 | 강조 텍스트 |

```csharp
// 전경색만
buffer.WriteText(5, 3, "Hello", ConsoleColor.Yellow);

// 전경색 + 배경색
buffer.SetCell(10, 5, 'X', ConsoleColor.White, ConsoleColor.Red);
```

### 5.5 씬 간 데이터 공유

**생성자 주입** — GameApp에서 공유 객체를 씬 생성 시 전달:

```csharp
private readonly SharedData _data;

private void ChangeToPlay()
{
    PlayScene play = new PlayScene(_data);
    // ...
}
```

**프로퍼티 + 이벤트** — 씬이 프로퍼티로 데이터를 가지고, 이벤트 발행 시 GameApp이 읽기:

```csharp
// TitleScene
public int SelectedDifficulty { get; private set; }
public event GameAction DifficultySelected;

// GameApp
title.DifficultySelected += () =>
{
    ChangeToPlay(title.SelectedDifficulty);
};
```

이벤트는 위로(씬→GameApp), 데이터는 아래로(생성자).

---

## 6. 자주 하는 실수

### `base(width, height)` 호출 누락

```csharp
// 잘못됨 — 컴파일 에러
public MyGame() { }

// 올바름
public MyGame() : base(40, 20) { }
```

### ChangeScene() 후에 이벤트 연결

```csharp
// 잘못됨 — ChangeScene 안에서 Load()가 이미 호출됨
_scenes.ChangeScene(title);
title.StartRequested += () => ChangeToPlay();

// 올바름 — 먼저 이벤트 연결
title.StartRequested += () => ChangeToPlay();
_scenes.ChangeScene(title);
```

### Console.Write 직접 사용

```csharp
// 잘못됨 — ScreenBuffer를 우회하면 화면이 깨짐
Console.Write("Hello");

// 올바름
buffer.WriteText(5, 5, "Hello");
```

### Draw()에서 상태 변경

```csharp
// 잘못됨
public override void Draw(ScreenBuffer buffer)
{
    _score++;
    buffer.WriteText(5, 5, "Score: " + _score);
}

// 올바름
public override void Update(float deltaTime) { _score++; }
public override void Draw(ScreenBuffer buffer)
{
    buffer.WriteText(5, 5, "Score: " + _score);
}
```

### using 문 누락

필요한 네임스페이스를 파일 상단에 직접 작성해야 합니다.

```csharp
// 잘못됨
namespace Framework.MyGame
{
    public class MyScene : Scene  // CS0246
}

// 올바름
using System;
using Framework.Engine;

namespace Framework.MyGame
{
    public class MyScene : Scene
}
```

### 범위 밖 좌표

에러가 나지 않고 무시됩니다. 화면에 안 나타나면 좌표를 확인하세요.

```csharp
// 화면이 40x20인데 x=50에 그리면 → 안 보임
buffer.WriteText(50, 10, "Hello");
```

### 추적 목록에 없는 키 사용

`Input`이 추적하는 키만 감지됩니다. `ConsoleKey.R` 같은 키를 쓰고 싶으면 `Input.cs`의 `s_trackedKeys`에 추가해야 합니다.

---

## 7. 빠른 참조

### 게임 루프 실행 순서

```
Run()
├── Console.CursorVisible = false
├── Console.Clear()
├── Initialize()
├── GameStarted 이벤트
└── while (_isRunning)
    ├── deltaTime 계산
    ├── Input.Poll()
    ├── Update(deltaTime)
    ├── Buffer.Clear()
    ├── Draw()
    ├── Buffer.Present()
    └── Thread.Sleep(남은 시간)
```

### 씬 전환 흐름

```
GameApp: ChangeToXxx()
    ├── new Scene()
    ├── scene.Event += handler
    └── _scenes.ChangeScene(scene)
        ├── 이전 씬.Unload()
        ├── _currentScene = scene
        ├── SceneChanged 이벤트 발행
        └── scene.Load()
```

### 전체 API 시그니처

| 클래스 | 멤버 | 시그니처 |
|--------|------|----------|
| **GameApp** | 생성자 | `protected GameApp(int width, int height)` |
| | Initialize | `protected abstract void Initialize()` |
| | Update | `protected abstract void Update(float deltaTime)` |
| | Draw | `protected abstract void Draw()` |
| | Quit | `protected void Quit()` |
| | Buffer | `protected ScreenBuffer Buffer { get; }` |
| | GameStarted | `public event GameAction GameStarted` |
| | GameStopped | `public event GameAction GameStopped` |
| **Scene** | Load | `public abstract void Load()` |
| | Update | `public abstract void Update(float deltaTime)` |
| | Draw | `public abstract void Draw(ScreenBuffer buffer)` |
| | Unload | `public abstract void Unload()` |
| | AddGameObject | `public void AddGameObject(GameObject gameObject)` |
| | RemoveGameObject | `public void RemoveGameObject(GameObject gameObject)` |
| | ClearGameObjects | `public void ClearGameObjects()` |
| | FindGameObject | `public GameObject FindGameObject(string name)` |
| | UpdateGameObjects | `protected void UpdateGameObjects(float deltaTime)` |
| | DrawGameObjects | `protected void DrawGameObjects(ScreenBuffer buffer)` |
| **GameObject** | 생성자 | `protected GameObject(Scene scene)` |
| | Name | `public string Name { get; set; }` — 기본값 `""` |
| | IsActive | `public bool IsActive { get; set; }` — 기본값 `true` |
| | Scene | `public Scene Scene { get; }` |
| | Update | `public abstract void Update(float deltaTime)` |
| | Draw | `public abstract void Draw(ScreenBuffer buffer)` |
| **SceneManager\<T\>** | CurrentScene | `public TScene CurrentScene { get; }` |
| | ChangeScene | `public void ChangeScene(TScene scene)` |
| | SceneChanged | `public event GameAction<TScene> SceneChanged` |
| **ScreenBuffer** | Width / Height | `public int Width { get; }` / `public int Height { get; }` |
| | SetCell | `public void SetCell(int x, int y, char ch, ConsoleColor color, ConsoleColor bgColor)` |
| | WriteText | `public void WriteText(int x, int y, string text, ConsoleColor color, ConsoleColor bgColor)` |
| | WriteTextCentered | `public void WriteTextCentered(int y, string text, ConsoleColor color, ConsoleColor bgColor)` |
| | WriteLines | `public void WriteLines(int x, int y, string[] lines, ConsoleColor color, ConsoleColor bgColor)` |
| | DrawHLine | `public void DrawHLine(int x, int y, int length, char ch, ConsoleColor color, ConsoleColor bgColor)` |
| | DrawVLine | `public void DrawVLine(int x, int y, int length, char ch, ConsoleColor color, ConsoleColor bgColor)` |
| | DrawBox | `public void DrawBox(int x, int y, int width, int height, ConsoleColor color, ConsoleColor bgColor)` |
| | FillRect | `public void FillRect(int x, int y, int width, int height, char ch, ConsoleColor color, ConsoleColor bgColor)` |
| | Clear | `public void Clear()` |
| | Present | `public void Present()` |
| **Input** | HasInput | `public static bool HasInput { get; }` |
| | Poll | `public static void Poll()` |
| | IsKey | `public static bool IsKey(ConsoleKey key)` — 누르는 동안 |
| | IsKeyDown | `public static bool IsKeyDown(ConsoleKey key)` — 누른 순간 |
| | IsKeyUp | `public static bool IsKeyUp(ConsoleKey key)` — 뗀 순간 |

### 주요 ConsoleKey 값

| 키 | ConsoleKey | 용도 |
|----|-----------|------|
| Enter | `ConsoleKey.Enter` | 확인, 시작 |
| Escape | `ConsoleKey.Escape` | 종료 |
| 방향키 | `UpArrow`, `DownArrow`, `LeftArrow`, `RightArrow` | 이동 |
| 숫자 | `D1`~`D9`, `NumPad1`~`NumPad9` | 선택 |
| 알파벳 | `A`~`Z` | 동작 (H: Hit, S: Stand 등) |
| 스페이스 | `ConsoleKey.Spacebar` | 동작 |

### 새 게임 최소 파일

```
MyGame/
├── MyGameApp.cs      ← GameApp 상속
├── TitleScene.cs     ← 시작 화면
└── PlayScene.cs      ← 게임 플레이
```

**체크리스트:**

- [ ] `MyGameApp : GameApp` + `base(width, height)`
- [ ] `SceneManager<Scene>` 필드
- [ ] `Initialize()` → `ChangeToTitle()`
- [ ] `Update()` → ESC 종료 + 현재 씬 위임
- [ ] `Draw()` → 현재 씬 위임
- [ ] `TitleScene : Scene` + `PlayScene : Scene`
- [ ] 각 씬에 이벤트(`GameAction`)
- [ ] `ChangeToXxx()`에서 이벤트 연결 → `ChangeScene()`
- [ ] `Program.cs`에 `new MyGameApp().Run();`
- [ ] 모든 파일에 `using System;` + `using Framework.Engine;`
