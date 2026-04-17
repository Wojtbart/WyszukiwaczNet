from configparser import ConfigParser
import os

_dir = os.path.dirname(os.path.abspath(__file__))
_plain_path     = os.path.join(_dir, "config.ini")
_encrypted_path = os.path.join(_dir, "config.ini.enc")


def _decrypt_ini(enc_path: str, password: str) -> str:
    try:
        from cryptography.hazmat.primitives.kdf.pbkdf2 import PBKDF2HMAC
        from cryptography.hazmat.primitives import hashes, padding
        from cryptography.hazmat.primitives.ciphers import Cipher, algorithms, modes
        from cryptography.hazmat.backends import default_backend
    except ImportError:
        raise RuntimeError("Install cryptography: pip install cryptography")

    data       = open(enc_path, "rb").read()
    salt       = data[:16]
    iv         = data[16:32]
    ciphertext = data[32:]

    kdf = PBKDF2HMAC(
        algorithm=hashes.SHA256(), length=32,
        salt=salt, iterations=100_000, backend=default_backend()
    )
    key = kdf.derive(password.encode("utf-8"))

    cipher    = Cipher(algorithms.AES(key), modes.CBC(iv), backend=default_backend())
    decryptor = cipher.decryptor()
    padded    = decryptor.update(ciphertext) + decryptor.finalize()

    unpadder = padding.PKCS7(128).unpadder()
    return (unpadder.update(padded) + unpadder.finalize()).decode("utf-8")


def read_db_config(section: str = "postgres") -> dict:
    parser = ConfigParser()

    # Prefer encrypted config if DB_CONFIG_KEY env var is set
    config_key = os.environ.get("DB_CONFIG_KEY")
    if config_key and os.path.exists(_encrypted_path):
        import io
        plain = _decrypt_ini(_encrypted_path, config_key)
        parser.read_string(plain)
    elif os.path.exists(_plain_path):
        parser.read(_plain_path)
    else:
        raise FileNotFoundError("No config.ini or config.ini.enc found.")

    if not parser.has_section(section):
        raise Exception(f"Section '{section}' not found in config.")

    return dict(parser.items(section))
