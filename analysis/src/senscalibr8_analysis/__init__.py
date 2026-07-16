"""Offline SensCalibr8 analysis package.

Production analysis behavior is intentionally deferred to later phases.
"""

__version__ = "0.1.0"

from .configuration import FrozenCalibrationConfiguration, ResearchConstants, load_frozen_calibration_configuration, load_research_constants

__all__ = ["FrozenCalibrationConfiguration", "ResearchConstants", "load_frozen_calibration_configuration", "load_research_constants"]
