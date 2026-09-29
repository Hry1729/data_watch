"""离线验证：无需数据库，不会写入演示数据。"""
import json
import unittest

from simulate_qc100 import build_frame, identifier, point_value, wave


class DemoTests(unittest.TestCase):
    def test_wave_has_full_range_and_point_variation(self):
        values = [wave(n / 10, 100, 12, 12, 100) for n in range(600)]
        self.assertGreaterEqual(min(values), 0)
        self.assertLessEqual(max(values), 100)
        self.assertLess(min(values), 2)
        self.assertGreater(max(values), 98)
        self.assertNotEqual(values[:10], [wave(n / 10, 101, 12, 12, 100) for n in range(10)])

    def test_types_keep_boolean_and_integer_contract(self):
        self.assertEqual(point_value(80.3, "Bool"), 1)
        self.assertEqual(point_value(20.1, "Bool"), 0)
        self.assertEqual(point_value(80.3, "Int"), 80)
        self.assertEqual(point_value(80.3, "Real"), 80.3)
        with self.assertRaises(ValueError):
            point_value(50, "String")

    def test_frame_honors_storage_and_shared_timestamp(self):
        points = [
            {"point_id": 1, "group_code": "g001", "td_column_name": "int1", "var_type": "Int", "is_storage": 1},
            {"point_id": 2, "group_code": "g002", "td_column_name": "bool1", "var_type": "Bool", "is_storage": 0},
        ]
        sql, latest = build_frame(points, 1700000000000, 0, 12, 12, 100, "device_data")
        self.assertIn("`device_data`.`qc100`", sql)
        self.assertIn("`device_data`.`qc100_q`", sql)
        self.assertNotIn("`bool1`", sql)
        self.assertIn("(1700000000000,1,0)", sql)
        self.assertNotIn("(1700000000000,2,0)", sql)
        for group in latest.values():
            for payload in group.values():
                value = json.loads(payload)
                self.assertEqual(value["t"], 1700000000000)
                self.assertEqual(value["q"], 0)
                self.assertIsInstance(value["v"], str)

    def test_reject_unsafe_sql_identifier(self):
        with self.assertRaises(ValueError):
            identifier("x`; DROP TABLE qc100")


if __name__ == "__main__":
    unittest.main()
