

**PROJECT PROPOSAL REPORT**

**SensCalibr8**

*A Personal Mouse-Sensitivity Calibration Laboratory for Valorant*

Prepared based on: "A Comprehensive Analysis of Optimal Technical Configurations

and Mechanical Performance Parameters in Valorant" (Consolidated Research Report)

Prepared for: Personal Development Use (Standalone, Single-User Desktop Application)

Document Type: Technical Project Design & Architecture Report

**Version: 3.0**

Revision Date: 14 July 2026

# **Revision Note — Version 3.0**

เอกสารฉบับนี้เป็นการปรับปรุงจาก Version 2.0 หลังจากผ่านกระบวนการ Peer Review โดยผู้ตรวจสอบอิสระ (Claude Sonnet) ซึ่งได้ทำการตรวจสอบแหล่งอ้างอิงงานวิจัยต้นฉบับโดยตรง และพบว่าข้อเสนอบางส่วนใน v2 มีการตีความงานวิจัยเกินขอบเขตที่มีหลักฐานรองรับจริง คณะผู้จัดทำจึงดำเนินการตรวจสอบซ้ำและปรับปรุงเนื้อหาให้มีความถูกต้องแม่นยำสูงสุด

**สรุปการเปลี่ยนแปลงหลักจาก v2 → v3:**

| รายการ | v2.0 | v3.0 | เหตุผล |
| :---- | :---- | :---- | :---- |
| **PSA Baseline eDPI** | 310 | 280 (Rollback) | แหล่งอ้างอิง v2 เป็น forum/บล็อกที่ไม่ผ่านการกลั่นกรอง ค่า 280 fact-check แล้วจากผู้เล่นมืออาชีพ |
| **Test Modes** | 5 โหมด (มี Scoped/ADS) | 4 โหมด | ขัด Design Philosophy 'Sensitivity only'; ADS Multiplier ตั้งค่าได้ในเกมอยู่แล้ว |
| **Role Profile** | มี (Duelist/Controller/Sentinel) | ตัดออก | ไม่มีหลักฐานงานวิจัยรองรับ เกินความจำเป็นสำหรับ personal tool |
| **Adaptation Period** | 50 shots คงที่ | 50% ของ shots แรกต่อค่า | ตัวเลขเดิมคลาดเคลื่อนจากสัดส่วนจริงใน IEEE CoG 2022 |
| **Submovement Count** | Penalty term ไม่มีนิยาม | คงไว้ \+ นิยาม algorithm ชัดเจน | ยืนยันถูกต้องจาก paper ต้นฉบับ |
| **Grip/Movement field** | อ้าง causal relationship | field เสริม \+ disclaimer | แหล่งอ้างอิงเป็น discussion paper ไม่ใช่ causal study |
| **Injury Risk Module** | ระบบวิเคราะห์ซับซ้อน | Warning Flag ทั่วไป | ลดความซับซ้อนให้เหมาะกับ personal tool |
| **Mousepad Constraint Validation** | มี | คงไว้ (อนุมัติ) | Physical logic ทั่วไป |
| **Performance Score per Mode** | ไม่มี | เพิ่มใหม่ | ต่อยอด design เดิม ไม่กระทบ scope |
| **ADS Multiplier** | Test Mode แยก | Field เดียว (Reference only) | ลด scope creep |

# **Table of Contents**

1\. Executive Summary

2\. Project Overview

3\. Technology Stack & System Architecture

4\. Multi-Profile System

5\. Test Mode Specifications

6\. Core Research Formulas & Calculations

7\. Testing Protocol (Execution Flow)

8\. Scientific Rigor & Confound Control

9\. Injury Risk & Ergonomic Safety Notice

10\. Data Integrity & Backup Strategy

11\. Database Schema

12\. Visualization & Reporting Layer

13\. Rejected Proposals & Rationale (v2 → v3)

14\. References

15\. Conclusion & Next Steps

# **1\. Executive Summary**

SensCalibr8 เป็นโปรแกรม Desktop Application สำหรับใช้งานส่วนตัว (Single-User, Local Execution) มีวัตถุประสงค์เพื่อค้นหาค่า Mouse Sensitivity ที่เหมาะสมที่สุดสำหรับผู้เล่นแต่ละคน โดยอ้างอิงจากงานวิจัยที่สังเคราะห์ขึ้นจากแหล่งข้อมูลผู้เชี่ยวชาญ Valorant จำนวน 4 แหล่ง (TenZ, CHARLATAN, Rem, และ Konpeki) ผสมผสานกับหลักการทางวิทยาศาสตร์การเคลื่อนไหว (Fitts's Law) มาตรฐานเชิงสถิติ และงานวิจัยเชิงวิชาการที่ผ่านการ peer review (IEEE CoG 2022\) เพื่อความน่าเชื่อถือของผลลัพธ์

โปรแกรมนี้ทำงานแยกอิสระจากตัวเกม Valorant โดยสมบูรณ์ (Standalone Aim-Testing Environment) เพื่อหลีกเลี่ยงความเสี่ยงด้าน Anti-Cheat (Vanguard) และมุ่งเน้นเฉพาะการวัดผลตัวแปรเดียว คือ Mouse Sensitivity เท่านั้น โดยไม่ครอบคลุมการตั้งค่าอื่น ๆ ของเกม (เช่น Graphics, Keybinds, ADS Multiplier) ซึ่งผู้ใช้สามารถตั้งค่าได้โดยตรงในเกมอยู่แล้ว

**\[v3.0\] เอกสารฉบับนี้ผ่านการทวนสอบ (Verification Pass) เพิ่มเติมจาก v2.0 โดยตัดข้อเสนอที่ scope ใหญ่เกินไปหรือขาดหลักฐานรองรับเพียงพอออก เพื่อคงความเป็นเครื่องมือส่วนตัวที่กระชับและแม่นยำทางวิทยาศาสตร์**

**1.1 Scope of This Document**

* Project Overview & Objectives  
* Technology Stack & System Architecture  
* Multi-Profile System Design  
* Test Mode Specifications (4 Modes)  
* Core Research Formulas & Calculations  
* Testing Protocol / Execution Flow  
* Scientific Rigor & Confound Control Mechanisms  
* Injury Risk & Ergonomic Safety Notice  
* Data Integrity & Backup Strategy  
* Database Schema  
* Visualization & Reporting Layer  
* Rejected Proposals & Rationale (Peer-Review Audit Trail)

# **2\. Project Overview**

## **2.1 Objective**

เป้าหมายหลักของโปรเจคคือการสร้างเครื่องมือที่ช่วยให้ผู้ใช้ค้นหา Sensitivity ที่เหมาะสมกับตัวเองได้อย่างเป็นระบบ (Data-Driven Approach) แทนการเดา (Guessing) หรือลอกเลียนค่าจากโปรเพลเยอร์โดยตรง ซึ่งไม่จำเป็นต้องเหมาะกับสรีระและรูปแบบการเล่นของผู้ใช้แต่ละคน

## **2.2 Design Philosophy**

* Focus exclusively on Sensitivity — no in-game settings are modified  
* Runs entirely offline, in a self-contained aim-testing environment — no interaction with Valorant's game client or Anti-Cheat (Vanguard)  
* Evidence-based: every formula and threshold used is traceable to either the consolidated research report or externally verified, peer-reviewed sources  
* Supports Multi-Profile usage — allows friends to test on the same machine with fully isolated data  
* Scope discipline (v3.0): features that introduce significant complexity without strong, verifiable evidence are deferred, not added speculatively

**\[v3.0\] เพิ่มหลักการ 'Scope Discipline' อย่างเป็นทางการ เนื่องจากพบว่า v2.0 มีแนวโน้ม Scope Creep จากข้อเสนอที่ไม่ผ่านการตรวจสอบแหล่งอ้างอิงอย่างเข้มงวด**

## **2.3 Out of Scope**

* Automatic modification of in-game settings (blocked intentionally due to Anti-Cheat risk)  
* Graphics, Keybinds, Audio, or Minimap configuration (covered by the original research report, not this application)  
* Automatic DPI detection from mouse firmware (technically infeasible without brand-specific SDKs)  
* Dedicated ADS/Scoped Sensitivity Test Mode — considered and rejected in v3.0 (see Section 13\)  
* Role-based Performance Weighting (Duelist/Controller/Sentinel) — considered and rejected in v3.0 (see Section 13\)

## **2.4 DPI Acquisition Method**

เนื่องจากค่า DPI ถูกจัดเก็บในเฟิร์มแวร์ของเมาส์และไม่สามารถอ่านได้โดยตรงผ่าน OS-level API ผู้ใช้จำเป็นต้องตรวจสอบค่า DPI ปัจจุบันจากซอฟต์แวร์เฉพาะของแบรนด์เมาส์ (เช่น SIGNO, Logitech G HUB, Razer Synapse) แล้วกรอกค่าดังกล่าวเข้าสู่ระบบด้วยตนเองในขั้นตอน Setup โดยระบบสำรอง (Fallback) จะเป็น Physical Ruler Test ซึ่งคำนวณ DPI จากระยะทางจริงที่วัดได้เทียบกับจำนวน counts ที่ระบบตรวจจับ

DPI (Physical Measurement Fallback) \=  
    mouse\_movement\_counts / (measured\_distance\_cm / 2.54)

# **3\. Technology Stack & System Architecture**

## **3.1 Technology Stack**

| Layer | Technology | Purpose |
| :---- | :---- | :---- |
| **Aim-Test Engine** | Unity (C\#) | Raw mouse input capture, 3D test scenes, in-app quick charting |
| **Data Storage** | SQLite | Local relational database; single-file, no server required |
| **Deep Analysis** | Python (pandas, matplotlib) | Statistical computation, chart generation, HTML report export |
| **Documentation** | Markdown (.md) | Research reference & rule files consumed by Codex during development |

## **3.2 Rationale for Technology Choices**

**3.2.1 Unity (C\#) over Python for the Test Engine**

Unity's Input System provides direct access to raw mouse delta values, bypassing OS-level cursor acceleration that commonly interferes with Python-based input libraries (e.g., pynput). This is essential for replicating the behavior of Raw Input Buffer as referenced in the source research.

**3.2.2 SQLite for Storage**

SQLite requires no server setup, supports complex analytical queries, and scales sufficiently for a single-user, long-term dataset (months to years of testing history).

**3.2.3 Python for Analysis**

Separating the analysis layer from the Unity engine allows independent iteration on statistical formulas (e.g., Performance Score weighting) without requiring changes to the test engine itself.

## **3.3 Timer Precision Requirement**

**\[v3.0 — Retained from v2.0\] ข้อกำหนดนี้ยังคงความสำคัญสูงสุดต่อความแม่นยำของงานวิจัย**

เนื่องจากการวัด Reaction Time และ Travel Time ต้องการความละเอียดระดับ milliseconds การใช้ Unity's Update() ที่ผูกกับ frame rate จะทำให้ timestamp คลาดเคลื่อนหากเฟรมเรตไม่นิ่ง ระบบจึงกำหนดกฎบังคับดังนี้:

* ต้องใช้ High-Resolution Timer (Time.realtimeSinceStartupAsDouble หรือ System.Diagnostics.Stopwatch) สำหรับจับเวลาทุกจุด ห้ามใช้ Time.time  
* ต้องอ่าน raw mouse delta ผ่าน Unity Input System โดยตรง ห้ามใช้ Input.GetAxis("Mouse X") ซึ่งมี built-in smoothing  
* ต้องตั้ง Target Frame Rate/VSync คงที่ระหว่างการทดสอบ เพื่อป้องกัน frame time variance ปนเข้าไปในข้อมูล

## **3.4 System Architecture Diagram**

┌─────────────────────┐         ┌──────────────────────┐  
│   Unity (C\#)          │  write  │   SQLite Database      │  
│  \- Aim Test Scenes x4  │ ──────► │  \- profiles              │  
│  \- Raw mouse capture   │         │  \- sessions               │  
│  \- In-app quick chart  │ ◄────── │  \- shots (raw log)         │  
└─────────────────────┘  read    │  \- tracking\_data             │  
                                   │  \- sensitivity\_tests           │  
                                   │  \- phase\_history                 │  
                                   │  \- cycles                          │  
                                   │  \- injury\_risk\_flags                 │  
                                   └──────────────────────┘  
                                             ▲  
                                             │ read  
                                   ┌──────────────────────┐  
                                   │  Python Analysis Layer │  
                                   │  \- pandas statistics     │  
                                   │  \- matplotlib charts       │  
                                   │  \- HTML / JSON / CSV export  │  
                                   └──────────────────────┘

# **4\. Multi-Profile System**

เนื่องจากโปรแกรมนี้อาจถูกใช้งานโดยผู้ใช้หลายคนบนเครื่องเดียวกัน (เช่น เพื่อนมาทดสอบ) ระบบจึงต้องรองรับ Profile แยกอิสระจากกันอย่างสมบูรณ์

## **4.1 Application Flow**

Launch Application  
  → Slot Selection Screen (list of existing profiles \+ "Create New Slot")  
  → Select / Create Profile  
  → Physical Profile Setup (Grip Style, Movement Strategy, Mousepad Size)  
  → Main Dashboard (scoped to selected profile\_id)  
  → Optional: Comparison Page (cross-profile analytics)

## **4.2 Physical Profile Attributes**

**\[v3.0\] เพิ่มขั้นตอนนี้จาก v2.0 แต่ปรับคำอธิบายให้ระมัดระวังเรื่องหลักฐานทางวิทยาศาสตร์**

ระบบเก็บข้อมูลสรีระ/รูปแบบการเล่นของผู้ใช้เป็น field เสริมในโปรไฟล์ เพื่อใช้ประกอบการตีความผลลัพธ์ในอนาคต:

* Grip Style (Fingertip / Palm / Claw / Hybrid)  
* Movement Strategy (Wrist-dominant / Arm-dominant / Hybrid)  
* Mousepad Size (Width x Height, cm) — ใช้สำหรับ Mousepad Constraint Validation (ดู Section 6.3)  
* ADS Sensitivity Multiplier (Reference only — ไม่มีการทดสอบแยก ดู Section 13.3)

**Disclaimer: ความสัมพันธ์ระหว่าง Grip Style/Movement Strategy กับค่า eDPI ที่เหมาะสม ยังไม่มีงานวิจัยเชิงสาเหตุ (causal study) รองรับ**

แหล่งอ้างอิงที่เคยถูกเสนอ (Dupuy, IJESPORTS 2023\) เป็นเพียง discussion paper ที่ระบุว่า 'mouse sensitivity in esports has not been deeply explored by researchers' และเรียกร้องให้มีการศึกษาเพิ่มเติม ไม่ได้พิสูจน์ว่า wrist-dominant player ควรใช้ eDPI สูงกว่า forearm-dominant player แต่อย่างใด ระบบจึงเก็บ field เหล่านี้ไว้เพียงเพื่อการบันทึกข้อมูลเชิงพรรณนา (descriptive) เท่านั้น ไม่นำไปใช้เป็นตัวแปรปรับน้ำหนักในสูตรคำนวณใดๆ

## **4.3 Key Features**

* Unlimited profile creation — each profile stores its own DPI, dominant hand, crosshair config, physical profile, and full test history  
* Comparison Page — compares Consistency, Reaction Time Tier, and Performance Score across profiles, always normalized via eDPI (raw in-game sensitivity values are never compared directly, since DPI differs between users)  
* Delete Slot — supports full profile deletion via cascading delete (ON DELETE CASCADE at the database level), with a mandatory confirmation dialog prior to execution, since the action is irreversible

# **5\. Test Mode Specifications**

**\[v3.0\] Rollback จาก 5 โหมด (v2.0) กลับเป็น 4 โหมดตามต้นฉบับ — Scoped/ADS Precision Mode ถูกตัดออก (เหตุผลละเอียดใน Section 13.3)**

ระบบทดสอบแบ่งออกเป็น 4 โหมดหลัก โดยแต่ละโหมดออกแบบมาเพื่อวัด "มิติ" ที่แตกต่างกันของ Sensitivity ตามแนวคิดที่ปรากฏในงานวิจัยต้นฉบับ

| \# | Mode | Description | Primary Metric | Research Basis |
| :---- | :---- | :---- | :---- | :---- |
| **1** | Flick – Close Range | Targets spawn around a central point at close distance, high spawn frequency (Aim Lab–style) | Reaction Time, Overflick / Underflick | High sensitivity favors flicky entry players |
| **2** | Flick – Far Range | Targets spawn far apart in both time and position | Travel Time (isolated from Reaction Time) | Low sensitivity favors methodical / precision aimers |
| **3** | Tracking | Continuously moving target (3 patterns: linear / curved / variable-speed) | Time-on-Target %, Tracking Deviation (SD) | Grip Tension 60–80% concept |
| **4** | Micro-Correction | Stationary, small-sized target near current crosshair position (5–20 px offset) | Micro-Adjustment Count, Final Precision Error, Submovement Count | PSA Method 'natural feel' concept \+ Submovement Analysis (IEEE CoG 2022\) |

## **5.1 Target Size Variation (Fitts's Law Compliance)**

ทุกโหมดต้องสุ่มขนาดเป้า (Small / Medium / Large) ควบคู่กับระยะทาง มิใช่ควบคุมเพียงระยะทางอย่างเดียว เพื่อให้สอดคล้องกับหลัก Fitts's Law ซึ่งกำหนด Index of Difficulty จากทั้งระยะทางและขนาดเป้าหมายร่วมกัน

Index of Difficulty (ID) \= log2(2 x Distance / Target Width)

## **5.2 Crosshair Consistency**

Crosshair ถูกล็อกเป็นค่าเดียวตายตัวตลอดการใช้งาน (Dot ขนาดเล็ก, สี Contrast สูง) เพื่อขจัดตัวแปรกวนที่ไม่เกี่ยวข้องกับ Sensitivity โดยเก็บ configuration ไว้ในระดับ Profile

## **5.3 Performance Score Per Test Mode**

**\[v3.0 — ใหม่\] เพิ่มตามข้อเสนอที่ได้รับการยอมรับจากการ Peer Review**

ระบบจะบันทึก Performance Score แยกตามแต่ละ Test Mode ก่อนนำมารวมเป็นคะแนนเฉลี่ยรวม เพื่อให้เห็นภาพชัดเจนว่าค่า Sensitivity ใดเหมาะกับสถานการณ์ใด (เช่น Sensitivity สูงอาจได้คะแนนดีใน Flick Close Range แต่ได้คะแนนต่ำใน Tracking) แนวคิดนี้สอดคล้องกับเหตุผลเดิมที่ออกแบบแยก Flick Close/Far ไว้ตั้งแต่ต้น และไม่ขัดกับ scope ของโปรเจค เนื่องจากเป็นเพียงการปรับโครงสร้างการจัดเก็บข้อมูล ไม่ใช่การเพิ่มโหมดทดสอบใหม่

# **6\. Core Research Formulas & Calculations**

## **6.1 Effective DPI (eDPI)**

eDPI \= In-game Sensitivity x Hardware DPI

## **6.2 PSA Method (Perfect Sensitivity Approximation)**

**\[v3.0\] Rollback: Baseline eDPI กลับเป็น 280 (จาก 310 ใน v2.0) — เหตุผลละเอียดใน Section 13.2**

Starting Sensitivity \= 280 / Hardware DPI  
eDPI Floor (hard minimum) \= 160

ค่า 280 มาจากการสังเคราะห์ข้อมูลผู้เล่นมืออาชีพ 4 แหล่ง (TenZ, CHARLATAN, Rem, Konpeki) ที่ผ่านการตรวจสอบข้อเท็จจริงแล้วในรายงานวิจัยต้นฉบับ ในขณะที่ค่า 310 ที่เคยเสนอใน v2.0 อ้างอิงจากข้อมูล community (vlr.gg forum, บล็อกทั่วไป) ซึ่งไม่ผ่านการกลั่นกรองเชิงวิชาการเพียงพอที่จะแทนที่ค่าเดิม

## **6.3 Physical Movement Distance (cm/360) & Mousepad Constraint Validation**

ใช้เป็นหน่วยเสริมคู่กับ eDPI เพื่อสะท้อนระยะทางจริงที่ต้องขยับมือบนแผ่นรองเมาส์ ซึ่งเชื่อมโยงกับหลักการยศาสตร์ (Ergonomics) และ Grip Tension ที่ระบุในงานวิจัยต้นฉบับ

cm/360 \= (2.54 x 360\) / (DPI x Sensitivity x 0.0022)

**\[v3.0 — ใหม่, อนุมัติจากการ Peer Review\] Mousepad Constraint Validation**

ระบบต้องตรวจสอบว่าค่า cm/360 ที่คำนวณได้ไม่เกินความกว้างจริงของแผ่นรองเมาส์ของผู้ใช้ (ค่าที่กรอกไว้ใน Physical Profile) หากเกิน ระบบต้องแจ้งเตือนผู้ใช้ทันที เนื่องจากผู้ใช้จะต้องยกเมาส์ (Mouse Lift) กลางการหมุน 360 องศา ซึ่งกระทบความแม่นยำของการเล็ง

IF cm\_360 \> mousepad\_width\_cm:  
    TRIGGER warning: "ค่า Sensitivity นี้ต้องยกเมาส์ระหว่างหมุน 360 องศา"

## **6.4 Performance Score Formula**

**\[v3.0\] Retained from v2.0 — Submovement Penalty Term ยืนยันถูกต้องจาก paper ต้นฉบับ (IEEE CoG 2022\)**

สูตรนี้ใช้ตัดสิน "Winner" ของแต่ละ Phase ในกระบวนการทดสอบ โดยได้ปรับน้ำหนักจากสูตรตั้งต้น เนื่องจากข้อมูลอ้างอิงจากการแข่งขันจริง (VCT Masters Reykjavik) แสดงให้เห็นว่าแม้แต่ผู้เล่นที่มี Headshot % สูงที่สุดในทัวร์นาเมนต์ (ScreaM) ก็ทำได้เพียง 33.12% เท่านั้น จึงไม่เหมาะให้เป็นตัวชี้วัดที่มีน้ำหนักสูงสุด นอกจากนี้ยังเพิ่ม Penalty Term จาก Submovement Count ซึ่งงานวิจัยยืนยันว่าจำนวนครั้งของการขยับแก้ไข (submovement) เพิ่มขึ้นตามสัดส่วนของ Sensitivity ที่สูงขึ้น

Performance Score \=  
    (Consistency         x 0.35) \+  
    (Accuracy%           x 0.30) \+  
    (Reaction Speed      x 0.20) \+  
    (Precision/Headshot% x 0.15) \-  
    (Submovement Penalty x 0.10)

Submovement Penalty \= normalized\_submovement\_count (0.0 \- 1.0 scale, higher \= more penalty)

**Submovement Count Algorithm (อ้างอิงจาก Boudaoud, Spjut & Kim, IEEE CoG 2022):**

* นับจุดเริ่มต้น submovement เมื่อ angular velocity เกิน threshold เริ่มต้น (\~8 องศา/วินาที)  
* นับจุดสิ้นสุด submovement เมื่อ angular velocity ลดต่ำกว่า threshold สิ้นสุด (\~4 องศา/วินาที)  
* ใช้ Refractory Period \~80ms เพื่อไม่นับ noise เป็น submovement ซ้ำ  
* แนะนำใช้ Butterworth low-pass filter กรอง raw mouse delta ก่อนคำนวณ velocity เพื่อลด noise จาก hardware

## **6.5 Reaction Time Benchmark Tiers**

| Tier | Reaction Time Range | Classification |
| :---- | :---- | :---- |
| **S** | \< 200 ms | Professional / Pro-level |
| **A** | 200 – 250 ms | Above Average |
| **B** | 250 – 350 ms | Average Gamer |
| **C** | 350 – 500 ms | Below Average |
| **D** | \> 500 ms | Needs Improvement |

*\* Benchmark values consolidated from multiple external sources referencing average human visual reaction time (\~250ms), average FPS player range (300–500ms), and professional esports player range (100–250ms / 150–180ms depending on source).*

## **6.6 Headshot % Usage Constraint**

เนื่องจากข้อมูลจริงจากทัวร์นาเมนต์ระดับสูงสุดแสดงค่าสูงสุดเพียง \~30–33% ระบบจึงกำหนดว่า Headshot %/Precision ต้องใช้เป็นตัวชี้วัดเสริมเท่านั้น (น้ำหนัก 0.15 ตามสูตรด้านบน) และห้ามตั้งเป้าหมาย (Target Threshold) ของผู้ใช้เกิน 35% เนื่องจากไม่สอดคล้องกับข้อมูลเชิงประจักษ์

# **7\. Testing Protocol (Execution Flow)**

## **7.1 Step 0 — Initial Setup**

* ผู้ใช้กรอก Hardware DPI (หรือใช้ Physical Ruler Test หากไม่ทราบค่า) และ Current In-game Sensitivity  
* ผู้ใช้กรอก Physical Profile (Grip Style, Movement Strategy, Mousepad Size)  
* ระบบคำนวณ PSA Baseline และเปรียบเทียบกับค่าปัจจุบันที่ผู้ใช้ใช้งานอยู่ พร้อม Mousepad Constraint Validation

## **7.2 Phase 1 — PSA Method (7 Test Values)**

* Counterbalancing: สุ่มลำดับการทดสอบทั้ง 7 ค่า เพื่อป้องกัน Order Effect  
* ขั้นต่ำ 30 shots ต่อค่า (Statistical Validity Rule)  
* Blind Testing: ไม่แสดงตัวเลข Sensitivity ระหว่างการทดสอบ เพื่อป้องกัน Placebo Effect  
* เลือก Winner จาก Performance Score พร้อมตรวจสอบ Statistical Significance ระหว่าง Top 2 candidates

**\[v3.0\] แก้ไข Adaptation Period — เดิม v2.0 ระบุ '50 shots คงที่' ซึ่งคลาดเคลื่อนจากงานวิจัยต้นฉบับ**

Adaptation Period: ระบบต้องทิ้งข้อมูล 50% ของ shots แรกในแต่ละค่า Sensitivity ที่ทดสอบ ไม่นำมาคำนวณ Performance Score (ไม่ใช่ตัวเลขตายตัว) อ้างอิงตามงานวิจัยต้นฉบับ (Boudaoud, Spjut & Kim, IEEE CoG 2022\) ที่ระบุว่า "we discard the first 50% of trials from each session as an adaptation period" เนื่องจากขนาด sample ของโปรเจคนี้เล็กกว่างานวิจัยต้นฉบับ (30+ shots ต่อค่า เทียบกับ 500 trials ต่อ session ในงานวิจัย) ระบบจึงปรับสัดส่วนตามขนาด sample จริงในแต่ละค่าที่ทดสอบ เช่น หากทดสอบ 30 shots จะทิ้ง 15 shots แรก

adaptation\_cutoff \= floor(total\_shots\_per\_value x 0.5)  
valid\_shots \= shots\[adaptation\_cutoff:\]  \# ใช้เฉพาะช่วงหลังนี้ในการคำนวณ Performance Score

## **7.3 Phase 2 — Progressive Narrowing (±10%)**

* ทดสอบ Winner Phase 1, Winner+10%, Winner−10%  
* ขั้นต่ำ 5 sessions ต่อค่า พร้อมเงื่อนไข: SD ของ Performance Score ต้องต่ำกว่า 10% ก่อนสรุปผล (เพิ่ม session ได้สูงสุด 10 ครั้ง หากยังไม่นิ่งพอ)

## **7.4 Phase 3 — Final Narrowing (±5%)**

* ทดสอบรอบ Winner Phase 2 ด้วยเงื่อนไขเดียวกับ Phase 2  
* ผลลัพธ์: Best Sensitivity (เบื้องต้น)

## **7.5 Step 4 — Performance Gate Check**

ประเมิน Best Sensitivity ด้วย Reaction Time และ Consistency เพื่อกำหนด Grade (S/A/B/C/D) หาก Grade อยู่ในระดับต่ำ ระบบจะแนะนำให้ฝึกฝนเพิ่มเติมที่ค่าดังกล่าวก่อน แทนที่จะสรุปว่าปัญหาอยู่ที่ตัว Sensitivity

## **7.6 Continuous Improvement Cycle**

Cycle N: Train at Best Sensitivity (5-10 sessions)  
   → Monitor Grade progression \+ Fatigue Detection  
   → If Grade plateaus  → Re-run Phase 1-3 (new baseline \= current value)  
   → If Grade improves   → Continue training at current value  
   → Proceed to Cycle N+1

# **8\. Scientific Rigor & Confound Control**

| Mechanism | Purpose |
| :---- | :---- |
| **Blind Testing** | Prevents Placebo Effect from knowing the tested sensitivity value |
| **Counterbalancing** | Prevents Order Effect (fatigue/warm-up bias across test sequence) |
| **Adaptation Period (50% discard)** | Prevents cold-start performance from contaminating results |
| **Outlier Detection** | Flags shots beyond 3 SD as accidental (not sensitivity-related) |
| **Fatigue Detection** | Flags within-session performance decline due to tiredness, not sensitivity |
| **Statistical Significance Test** | Prevents declaring a "Winner" based on statistical noise alone |

**\[v3.0\] "Warm-up Period (10-15 วินาที)" ของ v1.0 ถูกแทนที่ด้วย "Adaptation Period (50% discard)" อย่างถาวร ตามหลักฐานจากงานวิจัยต้นฉบับ**

# **9\. Injury Risk & Ergonomic Safety Notice**

**\[v3.0\] ลดความซับซ้อนจาก 'Injury Risk Module' เชิงวิเคราะห์ใน v2.0 ให้เป็น Warning Flag ทั่วไป เหมาะกับ personal tool มากขึ้น**

ระบบจะแสดงข้อความเตือนทั่วไป (ไม่ใช่การวินิจฉัยทางการแพทย์) เมื่อพบรูปแบบการตั้งค่าที่อาจเพิ่มความเสี่ยงด้านยศาสตร์ (Ergonomics) ตามหลักการทั่วไปที่ทราบกันในวงการอุปกรณ์เกมมิ่ง โดยไม่มีการวิเคราะห์เชิงลึกหรือให้คำแนะนำเฉพาะเจาะจงทางการแพทย์

**Warning Conditions (ตัวอย่าง):**

* eDPI ต่ำผิดปกติ (\< 200\) ร่วมกับ Movement Strategy \= Wrist-dominant → แสดงข้อความเตือนทั่วไปเรื่อง Repetitive Strain  
* cm/360 สูงเกิน mousepad\_width\_cm (Mousepad Constraint Violation) → แสดงข้อความเตือนเรื่องการยกเมาส์บ่อยเกินไป

IF edpi \< 200 AND movement\_strategy \== 'wrist':  
    SHOW warning\_flag("low\_edpi\_wrist\_strain")

IF cm\_360 \> mousepad\_width\_cm:  
    SHOW warning\_flag("mousepad\_constraint\_violation")

*หมายเหตุ: Warning เหล่านี้เป็นข้อความแจ้งเตือนทั่วไปเท่านั้น ไม่ใช่ระบบวิเคราะห์ความเสี่ยงเชิงการแพทย์ที่ซับซ้อน และไม่ควรใช้แทนคำแนะนำจากผู้เชี่ยวชาญด้านสุขภาพ*

# **10\. Data Integrity & Backup Strategy**

## **10.1 Export / Backup**

ระบบรองรับการ Export ข้อมูลเป็น JSON และ CSV แยกตาม Profile เพื่อป้องกันข้อมูลสูญหายในกรณีที่ไฟล์ SQLite เสียหาย

## **10.2 Formula Versioning**

ทุกผลลัพธ์ที่คำนวณจาก Performance Score จะถูกบันทึก formula\_version กำกับไว้เสมอ เพื่อป้องกันความสับสนหากมีการปรับปรุงน้ำหนักสูตรในอนาคต และเพื่อรักษาความสามารถในการเปรียบเทียบข้อมูลย้อนหลัง โดยเฉพาะการเปลี่ยนแปลงจาก v2.0 → v3.0 ในเอกสารนี้ ข้อมูลที่เก็บด้วยสูตรเก่า (baseline 310, ไม่มี Submovement Penalty) จะต้องถูก tag ด้วย formula\_version ที่แตกต่างจากข้อมูลใหม่เสมอ

# **11\. Database Schema**

**\[v3.0\] อัปเดต schema: เพิ่ม physical profile fields, injury\_risk\_flags table, ตัด scoped\_tests table ที่เคยเสนอใน v2.0 ออก**

profiles (  
    id INTEGER PRIMARY KEY,  
    name TEXT NOT NULL,  
    created\_date TEXT,  
    mouse\_dpi INTEGER,  
    dominant\_hand TEXT,  
    crosshair\_config TEXT,  
    grip\_style TEXT,               \-- Fingertip / Palm / Claw / Hybrid  
    movement\_strategy TEXT,        \-- Wrist / Arm / Hybrid  
    mousepad\_width\_cm REAL,  
    mousepad\_height\_cm REAL,  
    ads\_multiplier REAL,           \-- Reference only, no dedicated test mode  
    last\_active\_date TEXT  
)  
   |  
   | (ON DELETE CASCADE \-- applies to all tables below via profile\_id)  
   v

sessions (  
    id INTEGER PRIMARY KEY,  
    profile\_id INTEGER REFERENCES profiles(id),  
    date TEXT,  
    mode TEXT,                     \-- one of 4 Test Modes  
    duration\_sec INTEGER  
)

shots (  
    id INTEGER PRIMARY KEY,  
    session\_id INTEGER REFERENCES sessions(id),  
    profile\_id INTEGER REFERENCES profiles(id),  
    target\_id INTEGER,  
    distance\_zone TEXT,  
    target\_size TEXT,  
    spawn\_position TEXT,  
    spawn\_timestamp REAL,  
    first\_mouse\_movement\_timestamp REAL,  
    hit\_timestamp REAL,  
    hit\_position TEXT,  
    is\_hit BOOLEAN,  
    is\_outlier BOOLEAN,  
    is\_adaptation\_shot BOOLEAN,    \-- TRUE if within first 50% discard window  
    sensitivity\_value REAL,  
    initial\_offset\_distance REAL,  
    micro\_adjustment\_count INTEGER,  
    submovement\_count INTEGER,     \-- per Section 6.4 algorithm  
    final\_precision\_error REAL  
)

tracking\_data (  
    id INTEGER PRIMARY KEY,  
    session\_id INTEGER REFERENCES sessions(id),  
    profile\_id INTEGER REFERENCES profiles(id),  
    pattern\_type TEXT,  
    target\_speed REAL,  
    duration\_ms INTEGER,  
    deviation\_samples TEXT,  
    time\_on\_target\_ms INTEGER,  
    time\_on\_target\_percentage REAL  
)

sensitivity\_tests (  
    id INTEGER PRIMARY KEY,  
    profile\_id INTEGER REFERENCES profiles(id),  
    edpi REAL,  
    cm\_360 REAL,  
    avg\_performance\_score REAL,  
    performance\_score\_by\_mode TEXT,  \-- JSON: {mode: score} per Section 5.3  
    grade TEXT,  
    formula\_version TEXT,  
    phase INTEGER,  
    sample\_size INTEGER  
)

phase\_history (  
    id INTEGER PRIMARY KEY,  
    profile\_id INTEGER REFERENCES profiles(id),  
    phase\_number INTEGER,  
    winner\_edpi REAL,  
    timestamp TEXT  
)

cycles (  
    id INTEGER PRIMARY KEY,  
    profile\_id INTEGER REFERENCES profiles(id),  
    cycle\_number INTEGER,  
    start\_date TEXT,  
    end\_date TEXT,  
    outcome TEXT  
)

injury\_risk\_flags (  
    id INTEGER PRIMARY KEY,  
    profile\_id INTEGER REFERENCES profiles(id),  
    flag\_type TEXT,                \-- e.g. 'low\_edpi\_wrist\_strain'  
    triggered\_date TEXT,  
    edpi\_at\_trigger REAL,  
    acknowledged BOOLEAN DEFAULT 0  
)

# **12\. Visualization & Reporting Layer**

## **12.1 Layer 1 — Unity In-App (Immediate Feedback)**

* Bar Chart: Accuracy% per tested sensitivity within the current session

## **12.2 Layer 2 — Python Deep Analysis (Exported HTML Report)**

**\[v3.0\] เพิ่ม 1 กราฟใหม่ (Submovement Count vs eDPI) รวมเป็น 10 กราฟ ตัดกราฟที่เกี่ยวกับ Scoped Mode ออก**

| \# | Chart | Purpose |
| :---- | :---- | :---- |
| **1** | Sensitivity vs Performance Score Curve | Identify the peak eDPI value |
| **2** | Overflick vs Underflick Balance Chart | Indicate whether sensitivity should increase or decrease |
| **3** | Movement vs Stationary Error Graph | Compare error while stationary vs. moving |
| **4** | Progressive Narrowing Timeline | Visualize Phase 1→2→3 convergence |
| **5** | Consistency Trend Over Time | Track SD of error over the training history |
| **6** | Reaction Time Distribution | Histogram of reaction times per session |
| **7** | Performance Grade Timeline | Track Grade (S–D) alongside eDPI over time |
| **8** | Reaction Time vs Sensitivity Scatter Plot | Correlate eDPI with reaction speed |
| **9** | Submovement Count vs eDPI Curve | Visualize the monotonic relationship confirmed by IEEE CoG 2022 |
| **10** | Profile Comparison Chart | Cross-profile comparison, normalized via eDPI |

# **13\. Rejected Proposals & Rationale (v2 → v3 Audit Trail)**

Section นี้จัดทำขึ้นเพื่อความโปร่งใสในกระบวนการพัฒนาเอกสาร โดยบันทึกข้อเสนอทั้งหมดที่ถูกปฏิเสธหรือปรับลดจาก v2.0 พร้อมเหตุผลที่ตรวจสอบได้ เพื่อป้องกันการเสนอซ้ำในอนาคตโดยไม่มีหลักฐานใหม่ที่หนักแน่นกว่า

## **13.1 Role Profile (Duelist / Controller / Sentinel)**

ข้อเสนอเดิม: ถ่วงน้ำหนัก Performance Score ตาม Role ที่ผู้ใช้เล่น (เช่น Duelist ให้น้ำหนัก Reaction Speed สูงกว่า)

เหตุผลที่ปฏิเสธ: ไม่มีงานวิจัยที่ระบุตัวเลขน้ำหนักที่เหมาะสมสำหรับแต่ละ Role อย่างชัดเจน เป็นเพียง concept ที่เสนอโดยไม่มีหลักฐานรองรับ และเพิ่มความซับซ้อนของสูตรคำนวณอย่างมีนัยสำคัญ โดยไม่คุ้มค่ากับประโยชน์ที่ได้รับสำหรับเครื่องมือที่ใช้งานส่วนตัว (Personal Tool)

## **13.2 eDPI Baseline 280 → 310**

ข้อเสนอเดิม: เปลี่ยนค่า PSA Baseline จาก 280 เป็น 310 อ้างอิงจาก 'ข้อมูล Pro Player ปี 2026'

เหตุผลที่ปฏิเสธ: แหล่งอ้างอิงที่ใช้คือ vlr.gg (กระทู้ forum แฟนเกม) และบล็อกทั่วไป (rankedkings.com) ซึ่งไม่ใช่งานวิจัยเชิงวิชาการ วิธีเก็บข้อมูลและความแม่นยำไม่สามารถตรวจสอบได้ ในขณะที่ค่า 280 เดิมมาจากการสังเคราะห์และ fact-check จากคลิปผู้เชี่ยวชาญ 4 คนโดยตรง จึงมีความน่าเชื่อถือสูงกว่าอย่างมีนัยสำคัญ ค่า baseline จะยังคงเป็น 280 จนกว่าจะมีแหล่งอ้างอิงระดับวิชาการมายืนยันการเปลี่ยนแปลง

## **13.3 Test Mode 5 — Scoped/ADS Precision Mode**

ข้อเสนอเดิม: เพิ่ม Test Mode ที่ 5 สำหรับวัด Sensitivity ขณะ Aim Down Sight (ADS) พร้อม Scoped Sensitivity Multiplier formula แยก

เหตุผลที่ปฏิเสธ: ขัดกับ Design Philosophy ที่ตกลงกันไว้ตั้งแต่ต้นโปรเจคว่า 'Focus exclusively on Sensitivity — no in-game settings are modified' และ 'Sensitivity only scope' — ADS Sensitivity Multiplier เป็น setting ที่ผู้ใช้ตั้งค่าได้โดยตรงในเกม Valorant อยู่แล้ว การเพิ่ม Test Mode ใหม่ทั้งชุด (UI ใหม่, DB fields ใหม่, สูตรคำนวณใหม่) ถือเป็น Scope Creep ที่ไม่จำเป็นสำหรับเครื่องมือที่ประกาศไว้ว่า 'ใช้คนเดียว รันเอง' ระบบเก็บ ads\_multiplier ไว้เป็น field ข้อมูลอ้างอิงเฉยๆ ใน profiles table (ดู Section 4.2) โดยไม่มีระบบทดสอบแยก

## **13.4 Wrist-dominant → Higher Optimal eDPI (Causal Claim)**

ข้อเสนอเดิม: อ้างว่า wrist-dominant player มีค่า eDPI ที่เหมาะสมสูงกว่า forearm-dominant player อย่างมีนัยสำคัญทางสถิติ

เหตุผลที่ปฏิเสธ (ปรับลดระดับความเชื่อมั่น): แหล่งอ้างอิง (Dupuy, IJESPORTS 2023\) เป็น discussion paper ที่เรียกร้องให้มีการวิจัยเพิ่มเติม ไม่ใช่ study ที่พิสูจน์ความสัมพันธ์เชิงสาเหตุ ประโยคจริงในบทความระบุเพียงว่า sensitivity ยังไม่ถูกศึกษาอย่างลึกซึ้งในแวดวง esports ระบบจึงคงเก็บ field Movement Strategy ไว้ในฐานะข้อมูลเชิงพรรณนา (descriptive) เท่านั้น โดยไม่นำไปใช้ปรับน้ำหนักสูตรคำนวณใดๆ (ดู Section 4.2)

# **14\. References**

* Boudaoud, K., Spjut, J., & Kim, J. (2022). Analyzing Mouse Sensitivity Preferences in First-Person Aiming Tasks. IEEE Conference on Games (CoG) 2022\.  
* Dupuy, J. (2023). Mouse Sensitivity in Esports: A Discussion of Existing Gaps in Research. International Journal of Esports (IJESPORTS), Vol. 1\.  
* VCT Masters Reykjavik 2022 — Official Tournament Statistics (Headshot Percentage Data), Riot Games.  
* Consolidated Research Report: "A Comprehensive Analysis of Optimal Technical Configurations and Mechanical Performance Parameters in Valorant" (internal synthesis based on TenZ, CHARLATAN, Rem, and Konpeki settings breakdowns).  
* Fitts, P. M. (1954). The Information Capacity of the Human Motor System in Controlling the Amplitude of Movement. Journal of Experimental Psychology.

# **15\. Conclusion & Next Steps**

SensCalibr8 v3.0 เป็นเอกสารโครงการที่ผ่านกระบวนการทวนสอบเชิงหลักฐาน (Evidence Verification) อย่างเข้มงวด โดยคงหลักการ 'Sensitivity-only Scope' ไว้อย่างเคร่งครัด และปฏิเสธข้อเสนอที่ขาดแหล่งอ้างอิงเชิงวิชาการที่หนักแน่นเพียงพอ

**Next Steps ที่แนะนำสำหรับการพัฒนาต่อ:**

* เริ่มพัฒนา Unity Test Engine สำหรับ 4 Test Modes ตาม Timer Precision Requirement (Section 3.3)  
* ออกแบบและสร้าง SQLite Database ตาม Schema ใน Section 11  
* พัฒนา Python Analysis Layer สำหรับคำนวณ Performance Score และสร้างกราฟ 10 รายการ (Section 12\)  
* ทดสอบ Mousepad Constraint Validation และ Injury Risk Warning Flags กับข้อมูลจริงก่อนใช้งานเต็มรูปแบบ