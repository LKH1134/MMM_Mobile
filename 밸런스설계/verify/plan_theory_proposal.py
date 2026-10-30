# -*- coding: utf-8 -*-
"""check.py 어댑터: theory/model.py 의 'proposal' 안 (읽기만, 이론 파일은 수정하지 않음)."""
import importlib.util
import os

_p = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "theory", "model.py")
_spec = importlib.util.spec_from_file_location("theory_model", _p)
_m = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(_m)

NAME = "theory proposal (model.py " + str(int(os.path.getmtime(_p))) + ")"
_title, cost, _gold = _m.PLANS["proposal"]
enemy_gold = _gold
