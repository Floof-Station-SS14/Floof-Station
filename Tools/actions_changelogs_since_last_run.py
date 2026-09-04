#!/usr/bin/env python3

#
# Sends updates to a Discord webhook for new changelog entries since the last GitHub Actions publish run.
# Automatically figures out the last run and changelog contents with the GitHub API.
#

import io
import itertools
import os
import requests
import yaml
from typing import Any, Iterable

GITHUB_API_URL    = os.environ.get("GITHUB_API_URL", "https://api.github.com")
GITHUB_REPOSITORY = os.environ["GITHUB_REPOSITORY"]
GITHUB_RUN        = os.environ["GITHUB_RUN_ID"]
GITHUB_TOKEN      = os.environ["GITHUB_TOKEN"]
CHANGELOG_DIR     = os.environ["CHANGELOG_DIR"]
CHANGELOG_WEBHOOK = os.environ["CHANGELOG_WEBHOOK"]

# https://discord.com/developers/docs/resources/webhook
DISCORD_SPLIT_LIMIT = 2000

TYPES_TO_EMOJI = {
    "Fix":    "🐛",
    "Add":    "✨",
    "Remove": "❌",
    "Tweak":  "⚒️"
}

ChangelogEntry = dict[str, Any]

def main():
    if not CHANGELOG_WEBHOOK:
        return

    session = requests.Session()
    session.headers["Authorization"]        = f"Bearer {GITHUB_TOKEN}"
    session.headers["Accept"]               = "Accept: application/vnd.github+json"
    session.headers["X-GitHub-Api-Version"] = "2022-11-28"

    most_recent = get_most_recent_workflow(session)
    last_sha = most_recent['head_commit']['id']
    print(f"Last successful publish job was {most_recent['id']}: {last_sha}")
    last_changelog = yaml.safe_load(get_last_changelog(session, last_sha))
    with open(CHANGELOG_DIR, "r") as f:
        cur_changelog = yaml.safe_load(f)

    diff = diff_changelog(last_changelog, cur_changelog)
    send_to_discord(diff)


def get_most_recent_workflow(sess: requests.Session) -> Any:
    workflow_run = get_current_run(sess)
    past_runs = get_past_runs(sess, workflow_run)
    for run in past_runs['workflow_runs']:
        # First past successful run that isn't our current run.
        if run["id"] == workflow_run["id"]:
            continue

        return run


def get_current_run(sess: requests.Session) -> Any:
    resp = sess.get(f"{GITHUB_API_URL}/repos/{GITHUB_REPOSITORY}/actions/runs/{GITHUB_RUN}")
    resp.raise_for_status()
    return resp.json()


def get_past_runs(sess: requests.Session, current_run: Any) -> Any:
    """
    Get all successful workflow runs before our current one.
    """
    params = {
        "status": "success",
        "created": f"<={current_run['created_at']}"
    }
    resp = sess.get(f"{current_run['workflow_url']}/runs", params=params)
    resp.raise_for_status()
    return resp.json()


def get_last_changelog(sess: requests.Session, sha: str) -> str:
    """
    Use GitHub API to get the previous version of the changelog YAML (Actions builds are fetched with a shallow clone)
    """
    params = {
        "ref": sha,
    }
    headers = {
        "Accept": "application/vnd.github.raw"
    }

    resp = sess.get(f"{GITHUB_API_URL}/repos/{GITHUB_REPOSITORY}/contents/{CHANGELOG_DIR}", headers=headers, params=params)
    resp.raise_for_status()
    return resp.text


def diff_changelog(old: dict[str, Any], cur: dict[str, Any]) -> Iterable[ChangelogEntry]:
    """
    Find all new entries not present in the previous publish.
    """
    old_entry_ids = {e["id"] for e in old["Entries"]}
    return (e for e in cur["Entries"] if e["id"] not in old_entry_ids)


def get_discord_body(content: str):
    return {
        "content": content,
        # Do not allow any mentions.
        "allowed_mentions": {
            "parse": []
        },
        # SUPPRESS_EMBEDS
        "flags": 1 << 2
    }


def send_discord(content: str):
    body = get_discord_body(content)

    response = requests.post(CHANGELOG_WEBHOOK, json=body)
    response.raise_for_status()


def send_to_discord(entries: Iterable[ChangelogEntry]) -> None:
    if not CHANGELOG_WEBHOOK:
        print(f"No discord webhook URL found, skipping discord send")
        return

    message_content = io.StringIO()
    # We need to manually split messages to avoid discord's character limit
    # With that being said this isn't entirely robust
    # e.g. a sufficiently large CL breaks it, but that's a future problem

    for name, group in itertools.groupby(entries, lambda x: x["author"]):
        # Need to split text to avoid discord character limit
        group_content = io.StringIO()
        group_content.write(f"## {name}:\n")

        for entry in group:
            for change in entry["changes"]:
                emoji = TYPES_TO_EMOJI.get(change["type"], "❓")
                message = change["message"]

                labels = entry.get("labels") or []
                if EXPERIMENTAL_LABEL in labels:
                    emoji = f"{emoji}{EXPERIMENTAL_EMOJI}"

                message_lines.append(create_change_line(emoji, message, url))

    return message_lines


def split_message_lines(message_lines: list[str]) -> list[list[str]]:
    """Join message lines into chunks that are each below Discord's message length limit."""
    chunks = []
    chunk_lines = []
    chunk_length = 0

    for line in message_lines:
        line_length = len(line)
        if line_length > DISCORD_SPLIT_LIMIT:
            raise ValueError(
                f"Changelog line is too long for Discord after truncation: {line_length}"
            )

        new_chunk_length = chunk_length + line_length

        if new_chunk_length > DISCORD_SPLIT_LIMIT:
            if chunk_lines:
                chunks.append(chunk_lines)

            new_chunk_length = line_length
            chunk_lines = []

        chunk_lines.append(line)
        chunk_length = new_chunk_length

    if chunk_lines:
        chunks.append(chunk_lines)

    return chunks


def dump_debug_markdown(message_lines: list[str]):
    chunks = split_message_lines(message_lines)

    with DEBUG_DISCORD_DUMP_FILE.open("w", encoding="utf-8", newline="\n") as f:
        f.write("# Discord Changelog Debug Dump\n\n")
        f.write(
            f"Generated from `{DEBUG_CHANGELOG_FILE_OLD}` to `{CHANGELOG_FILE}`.\n\n"
        )

        if not chunks:
            f.write("_No changelog entries to send._\n")
            return

        for i, chunk_lines in enumerate(chunks, start=1):
            content = "".join(chunk_lines)
            f.write(
                f"<!-- Discord message break: chunk {i}/{len(chunks)}, {len(content)}/{DISCORD_SPLIT_LIMIT} characters -->\n\n"
            )
            f.write(f"## Discord Message {i}\n\n")
            f.write(content.lstrip("\n"))
            f.write("\n")

    print(f"Wrote Discord changelog debug dump to {DEBUG_DISCORD_DUMP_FILE}")


def send_message_lines(message_lines: list[str]):
    """Join a list of message lines into chunks that are each below Discord's message length limit, and send them."""
    chunks = split_message_lines(message_lines)

    for chunk_lines in chunks[:-1]:
        print("Split changelog and sending to discord")
        send_discord_webhook(chunk_lines)

    if chunks:
        print("Sending final changelog to discord")
        send_discord(message_text)


main()