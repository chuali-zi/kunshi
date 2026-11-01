# Unity MCP 配置

将 OpenCode 的 Unity MCP 从 stdio 改为 HTTP `http://127.0.0.1:8090/mcp`，避开 Unity 启动时 HTTP 与 stdio 传输竞争。已验证 OpenCode 连接、MCP 初始化、48 个工具发现及 `read_console` 实际调用成功。
