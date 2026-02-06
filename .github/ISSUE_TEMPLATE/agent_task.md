---
name: Copilot Agent Task
about: Assign a well-scoped investigation to the Copilot Coding Agent
title: "[Agent Task] <Short Description>"
labels: [agent]
assignees: [copilot]
---

## 🧠 Task Intent
What is the goal of this task? Be clear about what you expect Copilot to investigate or generate.

## 🔍 Scope / Input
- Part #: ABC123
- Compare: `testdata/mrp_log_sample_A.txt` vs `testdata/mrp_log_sample_B.txt`
- Focus: Why job disappeared

## ✅ Expected Output
Follow this structure:
A) Run Summary  
B) What Changed  
C) Most Likely Why (FACT vs INFERENCE)  
D) Log Evidence  
E) Epicor Screens to Check (if applicable)

## 🧪 Notes
- If logs are incomplete, say so
- Prioritize clarity and planner language
