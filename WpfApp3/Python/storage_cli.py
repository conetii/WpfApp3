import argparse
import json
import os
import shutil
import sys
import tempfile
import zipfile
from io import BytesIO
from pathlib import Path
from typing import Optional

try:
    from cryptography.exceptions import InvalidTag
    from cryptography.hazmat.primitives.kdf.scrypt import Scrypt
    from cryptography.hazmat.primitives.ciphers.aead import AESGCM
except ModuleNotFoundError:
    print(json.dumps({"ok": False, "error_code": "cryptography_missing", "message": "cryptography is not installed"}))
    sys.exit(2)

MAGIC = b"WSTORE1"
VERSION = 1
SALT_SIZE = 16
NONCE_SIZE = 12
KEY_SIZE = 32


def read_password() -> str:
    return sys.stdin.readline().rstrip("\r\n")


def derive_key(password: str, salt: bytes) -> bytes:
    kdf = Scrypt(salt=salt, length=KEY_SIZE, n=2**14, r=8, p=1)
    return kdf.derive(password.encode("utf-8"))


def build_payload(input_dir: str) -> bytes:
    buffer = BytesIO()
    with zipfile.ZipFile(buffer, "w", compression=zipfile.ZIP_DEFLATED) as archive:
        root = Path(input_dir)
        if root.exists():
            for path in sorted(root.rglob("*")):
                if path.is_dir():
                    continue
                relative_path = path.relative_to(root).as_posix()
                archive.write(path, relative_path)
    return buffer.getvalue()


def extract_payload(payload: bytes, output_dir: str) -> None:
    Path(output_dir).mkdir(parents=True, exist_ok=True)
    with zipfile.ZipFile(BytesIO(payload), "r") as archive:
        for member in archive.infolist():
            member_path = Path(member.filename)
            if member_path.is_absolute() or ".." in member_path.parts:
                raise ValueError("invalid archive entry")
            target_path = Path(output_dir, member.filename)
            target_path.parent.mkdir(parents=True, exist_ok=True)
            if member.is_dir():
                target_path.mkdir(parents=True, exist_ok=True)
                continue
            with archive.open(member, "r") as source, open(target_path, "wb") as target:
                shutil.copyfileobj(source, target)


def encrypt_payload(payload: bytes, password: str) -> bytes:
    salt = os.urandom(SALT_SIZE)
    nonce = os.urandom(NONCE_SIZE)
    key = derive_key(password, salt)
    ciphertext = AESGCM(key).encrypt(nonce, payload, MAGIC + bytes([VERSION]))
    return MAGIC + bytes([VERSION]) + salt + nonce + ciphertext


def decrypt_payload(archive_path: str, password: str) -> bytes:
    data = Path(archive_path).read_bytes()
    minimum_size = len(MAGIC) + 1 + SALT_SIZE + NONCE_SIZE + 16
    if len(data) < minimum_size:
        raise ValueError("archive_corrupted")
    magic = data[: len(MAGIC)]
    version = data[len(MAGIC)]
    if magic != MAGIC or version != VERSION:
        raise ValueError("archive_corrupted")
    offset = len(MAGIC) + 1
    salt = data[offset : offset + SALT_SIZE]
    offset += SALT_SIZE
    nonce = data[offset : offset + NONCE_SIZE]
    offset += NONCE_SIZE
    ciphertext = data[offset:]
    key = derive_key(password, salt)
    try:
        return AESGCM(key).decrypt(nonce, ciphertext, MAGIC + bytes([VERSION]))
    except InvalidTag as exception:
        raise PermissionError("invalid_password") from exception


def write_archive(archive_path: str, payload: bytes, password: str) -> None:
    archive_file = Path(archive_path)
    archive_file.parent.mkdir(parents=True, exist_ok=True)
    encrypted = encrypt_payload(payload, password)
    temp_file = archive_file.with_suffix(archive_file.suffix + ".tmp")
    temp_file.write_bytes(encrypted)
    temp_file.replace(archive_file)


def respond_success(message: Optional[str] = None) -> None:
    payload = {"ok": True}
    if message:
        payload["message"] = message
    print(json.dumps(payload))


def respond_error(code: str, message: str) -> None:
    print(json.dumps({"ok": False, "error_code": code, "message": message}))


def command_create(args: argparse.Namespace, password: str) -> None:
    temp_dir = tempfile.mkdtemp()
    try:
        write_archive(args.archive, build_payload(temp_dir), password)
    finally:
        shutil.rmtree(temp_dir, ignore_errors=True)
    respond_success("archive_created")


def command_verify(args: argparse.Namespace, password: str) -> None:
    decrypt_payload(args.archive, password)
    respond_success("archive_verified")


def command_unlock(args: argparse.Namespace, password: str) -> None:
    payload = decrypt_payload(args.archive, password)
    extract_payload(payload, args.output)
    respond_success("archive_unlocked")


def command_pack(args: argparse.Namespace, password: str) -> None:
    payload = build_payload(args.input)
    write_archive(args.archive, payload, password)
    respond_success("archive_packed")


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("command", choices=["create", "unlock", "pack", "verify"])
    parser.add_argument("--archive", required=True)
    parser.add_argument("--input")
    parser.add_argument("--output")
    args = parser.parse_args()
    password = read_password()

    try:
        if not password:
            raise ValueError("password_missing")
        if args.command == "create":
            command_create(args, password)
        elif args.command == "unlock":
            if not args.output:
                raise ValueError("missing_output")
            command_unlock(args, password)
        elif args.command == "pack":
            if not args.input:
                raise ValueError("missing_input")
            command_pack(args, password)
        else:
            command_verify(args, password)
        return 0
    except PermissionError:
        respond_error("invalid_password", "invalid password")
        return 3
    except FileNotFoundError:
        respond_error("archive_corrupted", "archive file was not found")
        return 4
    except zipfile.BadZipFile:
        respond_error("archive_corrupted", "archive payload is corrupted")
        return 5
    except ValueError as exception:
        code = str(exception)
        if code == "archive_corrupted":
            respond_error("archive_corrupted", "archive header is corrupted")
            return 6
        if code == "password_missing":
            respond_error("password_missing", "password is required")
            return 7
        if code == "missing_input":
            respond_error("missing_input", "input directory is required")
            return 8
        if code == "missing_output":
            respond_error("missing_output", "output directory is required")
            return 9
        respond_error("cli_failed", code)
        return 10
    except Exception as exception:
        respond_error("cli_failed", str(exception))
        return 11


if __name__ == "__main__":
    sys.exit(main())