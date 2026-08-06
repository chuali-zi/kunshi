# 2026-07-18 NIUA 管线搭建

核实 NIUA API 契约(presign 上传/mesh/rig/text2motion/sfx/jobs,rig 成功率~60% 需兜底)。搭建零依赖 Python 管线:niua_client.py + 三条生成脚本 + 三份 manifest,断点续跑防重复扣费,确定性 Idempotency-Key。Unity 侧新增 AudioCueAutoBinder(导入+按 AudioCueId 自动绑定)。dry-run 全通过。待办:用户提供 .env key 与角色参考图后执行生成。
