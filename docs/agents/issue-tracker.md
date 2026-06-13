# 问题追踪器：本地 Markdown

本仓库的 Issue 和 PRD 以 markdown 文件形式存放在 `.scratch/` 目录下。

## 约定

- 每个功能一个目录：`.scratch/<功能名>/`
- PRD 文件：`.scratch/<功能名>/PRD.md`
- 实施 Issue：`.scratch/<功能名>/issues/<NN>-<简述>.md`，从 `01` 开始编号
- 分类状态记录在每个 Issue 文件顶部 `Status:` 行中（参见 `triage-labels.md` 了解状态取值）
- 评论和对话历史追加到文件底部 `## 评论` 标题下

## 当 Skill 说"发布到问题追踪器"

在 `.scratch/<功能名>/` 下创建新文件（必要时新建目录）。

## 当 Skill 说"获取相关工单"

读取指定路径的文件。用户通常直接传入路径或 Issue 编号。
