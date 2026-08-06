from app.core.security import hash_password, verify_password


def test_local_password_hash_round_trip() -> None:
    encoded = hash_password("edge-password")

    assert verify_password("edge-password", encoded)
    assert not verify_password("wrong", encoded)