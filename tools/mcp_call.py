"""Call the trusted local Unity MCP server. Python 3.11+ and mcp/anyio required."""
import asyncio
import json
import os
import sys
import anyio
from mcp import ClientSession, StdioServerParameters
from mcp.client.stdio import stdio_client


async def main():
    command = os.environ.get('UNITY_MCP_SERVER', 'mcp-for-unity')
    env = dict(os.environ, DISABLE_TELEMETRY='1', UNITY_MCP_DISABLE_TELEMETRY='1')
    params = StdioServerParameters(command=command, args=['--transport', 'stdio'], env=env)
    response = None
    try:
        async with stdio_client(params) as (read, write):
            async with ClientSession(read, write) as session:
                await session.initialize()
                if len(sys.argv) < 2:
                    result = await session.list_tools()
                    response = [{'name': tool.name, 'schema': tool.inputSchema} for tool in result.tools]
                else:
                    result = await session.call_tool(sys.argv[1], json.loads(sys.argv[2]) if len(sys.argv) > 2 else {})
                    response = result.model_dump(mode='json')
    except* anyio.BrokenResourceError:
        # Some server versions emit tools/list_changed while stdio is shutting down.
        # Accept this one teardown race only after a complete response was received.
        # Call failures, cancellation and every other exception still propagate.
        if response is None:
            raise
        print('Unity MCP transport closed after receiving the complete response.', file=sys.stderr)
    print(json.dumps(response, indent=2), flush=True)
    if isinstance(response, dict) and response.get('isError'):
        return 1
    return 0


if __name__ == '__main__':
    sys.exit(asyncio.run(main()))
