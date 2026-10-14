import json
import unittest
from unittest.mock import patch

from writer_data_check import check_frame


class MappingConnection:
    def cursor(self, kind):
        return self

    def __enter__(self):
        return self

    def __exit__(self, *args):
        pass

    def execute(self, sql, parameters):
        assert sql.startswith("SELECT ")
        assert parameters == ("d1", "g001")

    def fetchall(self):
        return [{"point_id": 1, "td_column_name": "v1", "child_table": "d1"},
                {"point_id": 2, "td_column_name": "v2", "child_table": "d1"}]


class DataCheckTests(unittest.TestCase):
    def frame(self):
        return {"fields": {"device_code": "d1", "group_code": "g001", "ts": "1000",
                           "contract_version": "2", "values": json.dumps({"1": "3.5", "2": "4.2"}),
                           "qualities": json.dumps({"1": 192, "2": 0})}}

    def test_all_sampled_value_and_quality_points_match(self):
        def query(sql):
            self.assertTrue(sql.startswith("SELECT "))
            return [[1, 192], [2, 0]] if "SELECT var_id" in sql else [[3.5, 4.2]]
        with patch("writer_data_check.td_query", side_effect=query):
            result = check_frame(MappingConnection(), self.frame())
        self.assertEqual(result["status"], "pass")
        self.assertEqual((result["value_points"], result["quality_points"]), (2, 2))

    def test_missing_quality_is_a_failure(self):
        with patch("writer_data_check.td_query", side_effect=[[[3.5, 4.2]], [[1, 192]]]):
            result = check_frame(MappingConnection(), self.frame())
        self.assertEqual(result["status"], "fail")
        self.assertEqual(result["quality_mismatches"], 1)

    def test_not_landed_yet_is_not_reported_as_pass(self):
        with patch("writer_data_check.td_query", return_value=[]):
            result = check_frame(MappingConnection(), self.frame())
        self.assertEqual(result["status"], "not_tested")

    def test_invalid_reference_quality_cannot_silently_skip_quality_check(self):
        frame = self.frame()
        frame["fields"]["qualities"] = "{}"
        with patch("writer_data_check.td_query") as query:
            result = check_frame(MappingConnection(), frame)
        self.assertEqual(result["status"], "not_tested")
        query.assert_not_called()


if __name__ == "__main__":
    unittest.main()
