"""
ConfigTool for Python config files (config.ini)
Uses AES-256-CBC + PBKDF2 — same format as C# ConfigTool.
File format: [16 salt][16 IV][ciphertext]

Requirements: pip install cryptography
"""

import os
import sys
import getpass
import hashlib
import struct
from pathlib import Path

try:
    from cryptography.hazmat.primitives.kdf.pbkdf2 import PBKDF2HMAC
    from cryptography.hazmat.primitives import hashes, padding
    from cryptography.hazmat.primitives.ciphers import Cipher, algorithms, modes
    from cryptography.hazmat.backends import default_backend
except ImportError:
    print("Missing dependency. Run: pip install cryptography")
    sys.exit(1)


def derive_key(password: str, salt: bytes) -> bytes:
    kdf = PBKDF2HMAC(
        algorithm=hashes.SHA256(),
        length=32,
        salt=salt,
        iterations=100_000,
        backend=default_backend()
    )
    return kdf.derive(password.encode("utf-8"))


def encrypt_file(input_path: str, output_path: str, password: str) -> None:
    plain = Path(input_path).read_bytes()

    salt = os.urandom(16)
    iv   = os.urandom(16)
    key  = derive_key(password, salt)

    padder = padding.PKCS7(128).padder()
    padded = padder.update(plain) + padder.finalize()

    cipher    = Cipher(algorithms.AES(key), modes.CBC(iv), backend=default_backend())
    encryptor = cipher.encryptor()
    ciphertext = encryptor.update(padded) + encryptor.finalize()

    Path(output_path).write_bytes(salt + iv + ciphertext)
    print(f"Encrypted => {output_path}")


def decrypt_file(input_path: str, password: str) -> str:
    data       = Path(input_path).read_bytes()
    salt       = data[:16]
    iv         = data[16:32]
    ciphertext = data[32:]
    key        = derive_key(password, salt)

    cipher    = Cipher(algorithms.AES(key), modes.CBC(iv), backend=default_backend())
    decryptor = cipher.decryptor()
    padded    = decryptor.update(ciphertext) + decryptor.finalize()

    unpadder = padding.PKCS7(128).unpadder()
    plain    = unpadder.update(padded) + unpadder.finalize()
    return plain.decode("utf-8")


def read_password(prompt: str) -> str:
    return getpass.getpass(prompt)


def main():
    while True:
        print("\n=== ConfigTool (Python) ===")
        print("1. Encrypt config.ini")
        print("2. Decrypt .enc file (show content)")
        print("3. Decrypt .enc file (save to disk)")
        print("4. Exit")
        choice = input("\nChoice: ").strip()

        if choice == "4":
            break

        elif choice == "1":
            path = input("Input file [scripts/config/config.ini]: ").strip()
            if not path:
                path = "scripts/config/config.ini"
            if not os.path.exists(path):
                print("File not found.")
                continue
            out = input(f"Output file [{path}.enc]: ").strip()
            if not out:
                out = path + ".enc"
            pwd  = read_password("Password: ")
            pwd2 = read_password("Confirm password: ")
            if pwd != pwd2:
                print("Passwords do not match.")
                continue
            try:
                encrypt_file(path, out, pwd)
            except Exception as e:
                print(f"Error: {e}")

        elif choice in ("2", "3"):
            path = input("Encrypted file path: ").strip()
            if not os.path.exists(path):
                print("File not found.")
                continue
            pwd = read_password("Password: ")
            try:
                content = decrypt_file(path, pwd)
                if choice == "2":
                    print("\n--- Decrypted content ---")
                    print(content)
                    print("-------------------------")
                else:
                    default_out = path[:-4] if path.endswith(".enc") else path + ".dec"
                    out = input(f"Output file [{default_out}]: ").strip()
                    if not out:
                        out = default_out
                    Path(out).write_text(content, encoding="utf-8")
                    print(f"Decrypted => {out}")
            except Exception:
                print("Wrong password or corrupted file.")

        else:
            print("Unknown option.")


if __name__ == "__main__":
    main()
