---
type: DevLog
tags: [TechDebt, MVP, Networking]
status: Dirty
created: 2026-01-18
---
### 🗺️ 架构快照: Networking (Unity Relay)

#### 1. 现状 (The Dirty Reality)
- **实现**: 
  - 使用 `RelayBootstrap` 单体脚本处理所有连接逻辑 (GUI, Init, Auth, Relay)。
  - UI 直接使用 `OnGUI` (IMGUI)，性能差且难以维护，但实现最快。
  - `SimpleNetworkPlayer` 使用 `NetworkTransform` 进行位置同步，服务端权威模式，但在高延迟下可能有输入滞后 (没有客户端预测)。
  - **Input System Hack**: 在 `SimpleNetworkPlayer` 中直接轮询 `Keyboard.current`，没有使用 `InputAction` 资源，纯硬编码。
- **原因**: 
  - 快速验证联机玩法，不需要复杂的UI系统。
  - Relay 免费版足够 MVP 测试。
  - 项目已启用新版 Input System，旧版 API (`Input.GetAxis`) 被禁用，直接读键盘最快。

#### 2. 债权记录 (Tech Debt)
- [ ] **High Latency**: Relay 转发会增加 RTT，缺乏直连回退机制。
- [ ] **Hardcoded UI**: UI 逻辑耦合在网络管理脚本中。
- [ ] **No Prediction**: 移动手感完全依赖网络环境。
- [ ] **Security**: 匿名登录，无鉴权。
- [ ] **Input Coupling**: 输入逻辑硬编码在角色脚本中，无法自定义按键。

#### 3. 上帝视角 (God View)
> 仅在正式版重构。
- 引入 UGS (Unity Gaming Services) 完整大厅系统 (Lobby)。
- 拆分 `NetworkManager` 逻辑为 `ConnectionManager` 和 `UIManager`。
- 实现客户端预测移动 (Client Prediction & Reconciliation)。
- 使用 `PlayerInput` 组件重构输入系统。
