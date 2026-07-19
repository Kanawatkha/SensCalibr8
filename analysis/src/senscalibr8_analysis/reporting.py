"""Offline HTML evidence report for SensCalibr8 charts 1--10."""
from __future__ import annotations

import base64
import html
import io
import math
import os
import statistics
import tempfile
from collections import defaultdict
from dataclasses import dataclass
from pathlib import Path
from typing import Any, Iterable, Mapping

os.environ.setdefault("MPLCONFIGDIR", str(Path(tempfile.gettempdir()) / "senscalibr8-matplotlib"))

import matplotlib
matplotlib.use("Agg")
from matplotlib import pyplot as plt

REPORT_INPUT_VERSION = "sc8-html-report-input-v3"
REPORT_VERSION = "sc8-html-report-v3"
CHART_IDS = ("chart-1-sensitivity-performance", "chart-2-overflick-underflick",
             "chart-3-movement-stationary-error", "chart-4-progressive-narrowing",
             "chart-5-consistency-trend", "chart-6-reaction-time-distribution",
             "chart-7-performance-grade-timeline", "chart-8-reaction-time-sensitivity",
             "chart-9-submovement-edpi", "chart-10-profile-comparison")

@dataclass(frozen=True)
class ChartArtifact:
    chart_id: str
    title: str
    status: str
    source_rows: int
    image_data_uri: str | None
    note: str

def validate_report_input(document: Mapping[str, Any]) -> None:
    if document.get("reportInputVersion") != REPORT_INPUT_VERSION:
        raise ValueError("Unsupported or missing HTML report input version.")
    profile = document.get("profile")
    if not isinstance(profile, Mapping) or not profile.get("profileId") or not profile.get("name"):
        raise ValueError("A report input must identify its profile.")
    for collection in ("scores", "shots", "trackingErrors", "winners", "reactions", "profileComparisons", "outliers", "fatigue", "warnings", "significance"):
        if not isinstance(document.get(collection), list):
            raise ValueError(f"Report input field '{collection}' must be a list.")
    for score in document["scores"]:
        _required(score, "formulaVersion", "configurationVersion", "edpi", "performanceScore")
    for shot in document["shots"]:
        _required(shot, "configurationVersion", "date", "sensitivityValue", "edpi", "finalPrecisionErrorDeg")
    for row in document["trackingErrors"]:
        _required(row, "configurationVersion", "date", "sensitivityValue", "deviationRmsDeg")
    for reaction in document["reactions"]:
        _required(reaction, "sessionId", "date", "mode", "sensitivityValue", "reactionTimeMs", "configurationVersion")
    for comparison in document["profileComparisons"]:
        _required(comparison, "profileId", "profileName", "cycleNumber", "winnerEdpi")
    for outlier in document["outliers"]:
        _required(outlier, "phase", "mode", "sensitivityValue", "metricName", "inclusiveMean", "flaggedExcludedMean", "observationCount", "flaggedCount", "algorithmVersion", "configurationVersion")
    for warning in document["warnings"]:
        _required(warning, "flagType", "triggeredDate", "edpi", "acknowledged")
    for significance in document["significance"]:
        _required(significance, "phase", "candidateAEdpi", "candidateBEdpi", "alpha", "pValue", "isSignificant", "result", "method", "formulaVersion")

def build_chart_artifacts(document: Mapping[str, Any]) -> tuple[ChartArtifact, ...]:
    validate_report_input(document)
    return (_score_curve(document["scores"]), _overflick_underflick(document["shots"]),
            _movement_stationary_error(document["shots"], document["trackingErrors"]),
            _progressive_narrowing(document["winners"]),
            _consistency_trend(document["shots"], document["trackingErrors"]),
            _reaction_time_distribution(document["reactions"]),
            _performance_grade_timeline(document["scores"]),
            _reaction_time_sensitivity(document["reactions"]),
            _submovement_edpi(document["shots"]),
            _profile_comparison(document["profileComparisons"]))

def write_html_report(document: Mapping[str, Any], output_path: Path) -> Path:
    charts = build_chart_artifacts(document)
    profile = document["profile"]
    versions = sorted({row["formulaVersion"] for row in document["scores"]})
    configurations = sorted({row["configurationVersion"] for key in ("scores", "shots", "trackingErrors") for row in document[key]})
    sections = "".join(_chart_section(chart) for chart in charts)
    composition = "".join((
        _decision_section(document["winners"], document["significance"]),
        _authoritative_scores_section(document["scores"]),
        _outlier_section(document["outliers"]),
        _fatigue_section(document["fatigue"]),
        _warning_section(document["warnings"]),
        _methods_section(),
    ))
    output_path = Path(output_path)
    output_path.parent.mkdir(parents=True, exist_ok=True)
    output_path.write_text(
        "<!doctype html><html lang='en'><head><meta charset='utf-8'><title>SensCalibr8 evidence report</title>"
        "<style>body{font-family:Segoe UI,Arial,sans-serif;margin:2rem;max-width:1100px;color:#172033}section{border:1px solid #d8deea;border-radius:8px;padding:1rem;margin:1rem 0}img{max-width:100%;height:auto}.insufficient{background:#fff8e1}table{border-collapse:collapse;width:100%}th,td{border:1px solid #d8deea;padding:.45rem;text-align:left}th{background:#eef3fa}</style></head><body>"
        f"<h1>SensCalibr8 evidence report</h1><p>Profile: <strong>{html.escape(str(profile['name']))}</strong> (ID {html.escape(str(profile['profileId']))})</p>"
        f"<p>Report version: <code>{REPORT_VERSION}</code>; input version: <code>{REPORT_INPUT_VERSION}</code>.</p>"
        f"<p>Formula versions: <code>{html.escape(', '.join(versions) or 'No persisted scores')}</code><br>Configuration versions: <code>{html.escape(', '.join(configurations) or 'No source rows')}</code></p>"
        "<p>All charts are derived from persisted, post-adaptation evidence. Missing evidence is shown as insufficient rather than estimated.</p>"
        f"{composition}<h2>Evidence charts</h2>{sections}</body></html>", encoding="utf-8")
    return output_path

def _score_curve(scores: list[Mapping[str, Any]]) -> ChartArtifact:
    if not scores:
        return _insufficient(CHART_IDS[0], "Sensitivity vs Performance Score Curve", "No persisted complete-battery scores are available.")
    rows = sorted(scores, key=lambda row: _number(row["edpi"]))
    figure, axis = plt.subplots()
    axis.plot([_number(row["edpi"]) for row in rows], [_number(row["performanceScore"]) for row in rows], marker="o")
    axis.set(xlabel="eDPI", ylabel="Persisted Performance Score", title="Sensitivity vs Performance Score")
    axis.grid(True, alpha=0.25)
    return _chart(CHART_IDS[0], "Sensitivity vs Performance Score Curve", figure, len(rows), "Uses the persisted score; this report does not recalculate it.")

def _overflick_underflick(shots: list[Mapping[str, Any]]) -> ChartArtifact:
    over = under = omitted = 0
    for shot in shots:
        signed, azimuth = shot.get("signedAimErrorDeg"), _target_azimuth(shot.get("targetCenterPosition"))
        if signed is None or azimuth is None or azimuth == 0:
            omitted += 1
            continue
        adjusted = _number(signed) * (1 if azimuth > 0 else -1)
        if adjusted > 0: over += 1
        elif adjusted < 0: under += 1
    used = over + under
    if not used:
        return _insufficient(CHART_IDS[1], "Overflick vs Underflick Balance", "No shots have a non-zero intended horizontal direction and signed aim error.")
    figure, axis = plt.subplots()
    axis.bar(("Overflick", "Underflick"), (over, under), color=("#ca5b57", "#4d8fd0"))
    axis.set(ylabel="Post-adaptation shot count", title="Overflick vs Underflick")
    return _chart(CHART_IDS[1], "Overflick vs Underflick Balance", figure, used, f"Direction is inferred from stored non-zero target-center azimuth; {omitted} row(s) without direction were omitted. Raw signed error is unchanged.")

def _movement_stationary_error(shots: list[Mapping[str, Any]], tracking: list[Mapping[str, Any]]) -> ChartArtifact:
    stationary, moving = _means_by_sensitivity(shots, "finalPrecisionErrorDeg"), _means_by_sensitivity(tracking, "deviationRmsDeg")
    if not stationary or not moving:
        return _insufficient(CHART_IDS[2], "Movement vs Stationary Error", "Both stationary final-precision and moving tracking-deviation evidence are required.")
    figure, axis = plt.subplots()
    axis.plot(sorted(stationary), [stationary[key] for key in sorted(stationary)], marker="o", label="Stationary final precision error")
    axis.plot(sorted(moving), [moving[key] for key in sorted(moving)], marker="o", label="Moving tracking deviation RMS")
    axis.set(xlabel="Sensitivity value", ylabel="Angular error (degrees)", title="Movement vs Stationary Error")
    axis.legend(); axis.grid(True, alpha=0.25)
    return _chart(CHART_IDS[2], "Movement vs Stationary Error", figure, len(shots) + len(tracking), "Both series retain persisted angular-error units (degrees).")

def _progressive_narrowing(winners: list[Mapping[str, Any]]) -> ChartArtifact:
    if not winners:
        return _insufficient(CHART_IDS[3], "Progressive Narrowing Timeline", "No persisted phase winners are available.")
    rows = sorted(winners, key=lambda row: (_number(row["cycleNumber"]), _number(row["phase"])))
    labels = [f"C{int(_number(row['cycleNumber']))} P{int(_number(row['phase']))}" for row in rows]
    figure, axis = plt.subplots()
    axis.plot(range(len(rows)), [_number(row["edpi"]) for row in rows], marker="o")
    axis.set(xlabel="Cycle / phase winner", ylabel="Winner eDPI", title="Progressive Narrowing Timeline")
    axis.set_xticks(range(len(labels)), labels, rotation=35, ha="right"); axis.grid(True, alpha=0.25); figure.tight_layout()
    return _chart(CHART_IDS[3], "Progressive Narrowing Timeline", figure, len(rows), "Shows persisted phase winners in cycle and phase order.")

def _consistency_trend(shots: list[Mapping[str, Any]], tracking: list[Mapping[str, Any]]) -> ChartArtifact:
    grouped: dict[str, list[float]] = defaultdict(list)
    for row in shots: grouped[str(row["date"])].append(_number(row["finalPrecisionErrorDeg"]))
    for row in tracking: grouped[str(row["date"])].append(_number(row["deviationRmsDeg"]))
    points = [(date, statistics.stdev(values)) for date, values in sorted(grouped.items()) if len(values) >= 2]
    if not points:
        return _insufficient(CHART_IDS[4], "Consistency Trend Over Time", "At least two post-adaptation angular-error observations are required for a time point.")
    figure, axis = plt.subplots()
    axis.plot([date for date, _ in points], [value for _, value in points], marker="o")
    axis.set(xlabel="Session date", ylabel="Sample SD of angular error (degrees)", title="Consistency Trend Over Time")
    axis.tick_params(axis="x", rotation=35); axis.grid(True, alpha=0.25); figure.tight_layout()
    return _chart(CHART_IDS[4], "Consistency Trend Over Time", figure, sum(len(grouped[date]) for date, _ in points), "Lower sample standard deviation indicates greater consistency; no score is recalculated.")

def _reaction_time_distribution(reactions: list[Mapping[str, Any]]) -> ChartArtifact:
    if not reactions:
        return _insufficient(CHART_IDS[5], "Reaction Time Distribution", "No post-adaptation primary reaction-time observations are available.")
    grouped: dict[str, list[float]] = defaultdict(list)
    for row in reactions:
        grouped[f"Session {int(_number(row['sessionId']))}"] .append(_number(row["reactionTimeMs"]))
    figure, axis = plt.subplots()
    axis.hist(list(grouped.values()), bins="auto", label=list(grouped), histtype="step")
    axis.set(xlabel="Primary reaction/travel/correction time (ms)", ylabel="Observation count", title="Reaction Time Distribution by Session")
    if len(grouped) > 1:
        axis.legend()
    return _chart(CHART_IDS[5], "Reaction Time Distribution", figure, len(reactions), "Uses persisted primary-time observations per Database Session. Far shots with no recorded movement onset are omitted rather than assigned the scoring fallback.")

def _performance_grade_timeline(scores: list[Mapping[str, Any]]) -> ChartArtifact:
    rows = [row for row in scores if row.get("grade")]
    if not rows:
        return _insufficient(CHART_IDS[6], "Performance Grade Timeline", "No persisted Grade assignments are available.")
    rank = {"D": 0, "C": 1, "B": 2, "A": 3, "S": 4}
    rows = sorted(rows, key=lambda row: str(row["completedDate"]))
    valid = [row for row in rows if row["grade"] in rank]
    if not valid:
        return _insufficient(CHART_IDS[6], "Performance Grade Timeline", "Persisted Grade values are not in the approved S-D set.")
    figure, axis = plt.subplots()
    axis.plot(range(len(valid)), [rank[row["grade"]] for row in valid], marker="o", label="Persisted Grade")
    axis.set(xlabel="Completed battery", ylabel="Grade", title="Performance Grade Timeline")
    axis.set_yticks(range(len(rank)), ("D", "C", "B", "A", "S"))
    labels = [f"{row['completedDate']} / {row['edpi']}" for row in valid]
    axis.set_xticks(range(len(labels)), labels, rotation=35, ha="right")
    axis.grid(True, alpha=0.25); figure.tight_layout()
    return _chart(CHART_IDS[6], "Performance Grade Timeline", figure, len(valid), "Shows the persisted final Grade with its eDPI in chronological completed-battery order; the report does not assign Grades.")

def _reaction_time_sensitivity(reactions: list[Mapping[str, Any]]) -> ChartArtifact:
    if not reactions:
        return _insufficient(CHART_IDS[7], "Reaction Time vs Sensitivity Scatter Plot", "No post-adaptation primary reaction-time observations are available.")
    figure, axis = plt.subplots()
    modes = sorted({str(row["mode"]) for row in reactions})
    for mode in modes:
        rows = [row for row in reactions if row["mode"] == mode]
        axis.scatter([_number(row["sensitivityValue"]) for row in rows], [_number(row["reactionTimeMs"]) for row in rows], label=mode)
    axis.set(xlabel="Sensitivity value", ylabel="Primary reaction/travel/correction time (ms)", title="Reaction Time vs Sensitivity")
    axis.legend()
    axis.grid(True, alpha=0.25)
    return _chart(CHART_IDS[7], "Reaction Time vs Sensitivity Scatter Plot", figure, len(reactions), "Plots raw persisted primary-time observations. It is descriptive and does not establish causation.")

def _submovement_edpi(shots: list[Mapping[str, Any]]) -> ChartArtifact:
    rows = [row for row in shots if row.get("submovementCount") is not None]
    if not rows:
        return _insufficient(CHART_IDS[8], "Submovement Count vs eDPI Curve", "No post-adaptation hit rows with a persisted Submovement Count are available.")
    grouped: dict[float, list[float]] = defaultdict(list)
    for row in rows:
        grouped[_number(row["edpi"])].append(_number(row["submovementCount"]))
    figure, axis = plt.subplots()
    values = sorted(grouped)
    axis.plot(values, [statistics.fmean(grouped[key]) for key in values], marker="o")
    axis.set(xlabel="eDPI", ylabel="Mean persisted Submovement Count", title="Submovement Count vs eDPI")
    axis.grid(True, alpha=0.25)
    return _chart(CHART_IDS[8], "Submovement Count vs eDPI Curve", figure, len(rows), "Uses only persisted post-adaptation Submovement Counts. Misses and other null raw counts are omitted rather than converted to zero.")

def _profile_comparison(comparisons: list[Mapping[str, Any]]) -> ChartArtifact:
    latest: dict[int, Mapping[str, Any]] = {}
    for row in comparisons:
        profile_id = int(_number(row["profileId"]))
        if profile_id not in latest or _number(row["cycleNumber"]) > _number(latest[profile_id]["cycleNumber"]):
            latest[profile_id] = row
    rows = [latest[key] for key in sorted(latest)]
    if len(rows) < 2:
        return _insufficient(CHART_IDS[9], "Profile Comparison Chart", "At least two profiles with a persisted Phase 3 Winner are required for cross-profile comparison.")
    figure, axis = plt.subplots()
    axis.bar([str(row["profileName"]) for row in rows], [_number(row["winnerEdpi"]) for row in rows])
    axis.set(xlabel="Profile", ylabel="Phase 3 Winner eDPI", title="Profile Comparison (eDPI normalized)")
    axis.tick_params(axis="x", rotation=35); axis.grid(True, axis="y", alpha=0.25); figure.tight_layout()
    return _chart(CHART_IDS[9], "Profile Comparison Chart", figure, len(rows), "Compares only each profile's latest persisted Phase 3 Winner eDPI; it makes no causal or skill-ranking claim.")

def _means_by_sensitivity(rows: Iterable[Mapping[str, Any]], value_key: str) -> dict[float, float]:
    grouped: dict[float, list[float]] = defaultdict(list)
    for row in rows: grouped[_number(row["sensitivityValue"])].append(_number(row[value_key]))
    return {key: statistics.fmean(values) for key, values in grouped.items()}

def _target_azimuth(position: Any) -> float | None:
    if not isinstance(position, str): return None
    try: value = float(position.split(",", 1)[0].strip())
    except (ValueError, IndexError): return None
    return value if math.isfinite(value) else None

def _chart(chart_id: str, title: str, figure: Any, source_rows: int, note: str) -> ChartArtifact:
    buffer = io.BytesIO()
    figure.savefig(buffer, format="png", dpi=144, bbox_inches="tight"); plt.close(figure)
    return ChartArtifact(chart_id, title, "ready", source_rows, "data:image/png;base64," + base64.b64encode(buffer.getvalue()).decode("ascii"), note)

def _insufficient(chart_id: str, title: str, note: str) -> ChartArtifact:
    return ChartArtifact(chart_id, title, "insufficient_data", 0, None, note)

def _chart_section(chart: ChartArtifact) -> str:
    body = f"<img alt='{html.escape(chart.title)}' src='{chart.image_data_uri}'>" if chart.image_data_uri else "<p class='insufficient'>Insufficient data: chart not estimated.</p>"
    return f"<section id='{chart.chart_id}' data-status='{chart.status}'><h2>{html.escape(chart.title)}</h2>{body}<p>{html.escape(chart.note)}</p><p>Source rows: {chart.source_rows}</p></section>"

def _decision_section(winners: list[Mapping[str, Any]], significance: list[Mapping[str, Any]]) -> str:
    phase_three = [row for row in winners if _number(row["phase"]) == 3]
    if phase_three:
        winner = max(phase_three, key=lambda row: _number(row["cycleNumber"]))
        decision = f"Latest persisted Phase 3 Winner: eDPI {_display(winner['edpi'])}, sensitivity {_display(winner['sensitivityValue'])}, cycle {_display(winner['cycleNumber'])}."
    else:
        decision = "No persisted Phase 3 Winner is available. This report does not infer whether the protocol is incomplete, tied, or otherwise unresolved."
    significance_rows = _table(("Phase", "Candidate A eDPI", "Candidate B eDPI", "p-value", "Result", "Method"), (
        (_display(row["phase"]), _display(row["candidateAEdpi"]), _display(row["candidateBEdpi"]), _display(row["pValue"]), row["result"], row["method"]) for row in significance))
    return f"<section id='decision-context'><h2>Winner and tie context</h2><p>{html.escape(decision)}</p>{_evidence_or_table(significance_rows, bool(significance), 'No persisted confirmatory significance decision is available.')}</section>"

def _authoritative_scores_section(scores: list[Mapping[str, Any]]) -> str:
    table = _table(("Completed", "Cycle", "Phase", "eDPI", "Performance Score", "Grade", "Formula", "Configuration"), (
        (row["completedDate"], _display(row["cycleId"]), _display(row["phase"]), _display(row["edpi"]), _display(row["performanceScore"]), row.get("grade") or "Not assigned", row["formulaVersion"], row["configurationVersion"]) for row in scores))
    return f"<section id='authoritative-results'><h2>Inclusive authoritative results</h2><p>These are persisted complete-battery results. Statistical outlier flags remain included unless a separately documented data-quality exclusion was authorized.</p>{_evidence_or_table(table, bool(scores), 'No persisted complete-battery result is available.')}</section>"

def _outlier_section(outliers: list[Mapping[str, Any]]) -> str:
    table = _table(("Phase", "Mode", "Metric", "Sensitivity", "Inclusive mean", "Flagged-row-excluded mean", "Observations", "Flagged", "Algorithm"), (
        (_display(row["phase"]), row["mode"], row["metricName"], _display(row["sensitivityValue"]), _display(row["inclusiveMean"]), _display(row["flaggedExcludedMean"]), _display(row["observationCount"]), _display(row["flaggedCount"]), row["algorithmVersion"]) for row in outliers))
    return f"<section id='outlier-sensitivity-analysis'><h2>Outlier sensitivity analysis</h2><p>Flagged-row-excluded values are a parallel diagnostic only and do not replace the inclusive authoritative result.</p>{_evidence_or_table(table, bool(outliers), 'No persisted outlier analysis run is available.')}</section>"

def _fatigue_section(fatigue: list[Mapping[str, Any]]) -> str:
    rows = [row for row in fatigue if row.get("fatigueFlag") or row.get("scoreChangePercentage") is not None]
    table = _table(("Session", "Date", "Mode", "Score change", "Fatigue flag", "Algorithm"), (
        (_display(row["sessionId"]), row["date"], row["mode"], _display(row["scoreChangePercentage"]) if row.get("scoreChangePercentage") is not None else "Undefined", "Flagged" if row["fatigueFlag"] else "Not flagged", row.get("algorithmVersion") or "Not recorded") for row in rows))
    return f"<section id='fatigue-status'><h2>Fatigue status</h2><p>Fatigue is informational and does not exclude a session from Winner selection.</p>{_evidence_or_table(table, bool(rows), 'No persisted fatigue evaluation is available.')}</section>"

def _warning_section(warnings: list[Mapping[str, Any]]) -> str:
    table = _table(("Warning", "Triggered", "eDPI", "Acknowledged"), (
        (row["flagType"], row["triggeredDate"], _display(row["edpi"]), "Yes" if row["acknowledged"] else "No") for row in warnings))
    return f"<section id='ergonomic-warnings'><h2>Ergonomic warnings</h2><p>Warnings are informational, non-diagnostic, and do not block testing or alter results.</p>{_evidence_or_table(table, bool(warnings), 'No ergonomic warning is recorded.')}</section>"

def _methods_section() -> str:
    return "<section id='scientific-notes'><h2>Scientific notes and scope</h2><ul><li>Only post-adaptation evidence enters report charts and tables.</li><li>Scores, Grades, eDPI, and Winners are persisted C# Data/Service outputs; Python does not recompute them.</li><li>The report is offline and is not an Import/Restore backup.</li><li>Profile comparison is descriptive only and does not make causal or skill-ranking claims.</li></ul></section>"

def _table(headers: tuple[str, ...], rows: Iterable[Iterable[Any]]) -> str:
    rendered = "".join("<tr>" + "".join(f"<td>{html.escape(str(value))}</td>" for value in row) + "</tr>" for row in rows)
    return "<table><thead><tr>" + "".join(f"<th>{html.escape(header)}</th>" for header in headers) + "</tr></thead><tbody>" + rendered + "</tbody></table>"

def _evidence_or_table(table: str, available: bool, message: str) -> str:
    return table if available else f"<p class='insufficient'>Insufficient data: {html.escape(message)}</p>"

def _display(value: Any) -> str:
    return format(_number(value), ".12g")

def _number(value: Any) -> float:
    number = float(value)
    if not math.isfinite(number): raise ValueError("Report input contains a non-finite number.")
    return number

def _required(row: Mapping[str, Any], *keys: str) -> None:
    for key in keys:
        if key not in row or row[key] is None or row[key] == "": raise ValueError(f"Report input requires '{key}'.")
