#!/usr/bin/env python3
"""Real isolated runtime acceptance; never modifies production application state."""
import argparse
import json
import os
import tempfile
import threading
from pathlib import Path
from runner import Runner, DOCKER, command
from policy import redact


def smoke(runner):
    network = json.loads(command([*DOCKER, 'network', 'inspect', runner.config['agent_network']], runner.root))[0]
    gateway = network['IPAM']['Config'][0]['Gateway']
    probe = r'''
const net=require('net');
function connect(host,port) { return new Promise(resolve=>{
 const s=net.connect({host,port});s.setTimeout(2500);
 s.on('connect',()=>{s.destroy();resolve(true)});
 s.on('timeout',()=>{s.destroy();resolve(false)});s.on('error',()=>resolve(false));
});}
function proxy(target) { return new Promise((resolve,reject)=>{
 const s=net.connect({host:'massar-repair-proxy',port:3128});s.setTimeout(10000);
 s.on('connect',()=>s.write('CONNECT '+target+' HTTP/1.1\r\nHost: '+target+'\r\n\r\n'));
 s.once('data',data=>{s.destroy();resolve(data.toString().split('\r\n')[0])});
 s.on('timeout',()=>{s.destroy();reject(Error('proxy timeout'))});s.on('error',reject);
});}
(async()=>{
 if(await connect(process.argv[1],22)) throw Error('Host SSH is reachable from agent');
 if(await connect('1.1.1.1',443)) throw Error('Unrestricted outbound connection succeeded');
 if(!(await proxy('example.com:443')).includes('403')) throw Error('Proxy allowed unrelated domain');
 if(!(await proxy(process.argv[1]+':443')).includes('403')) throw Error('Proxy allowed private destination');
 if(!(await proxy('api.openai.com:443')).includes('200')) throw Error('Codex destination unavailable');
 console.log('network isolation and allowed Codex connection passed');
})().catch(e=>{console.error(e.message);process.exitCode=1});
'''
    result = command([*DOCKER, 'run', '--rm', '--pull=never', '--network', runner.config['agent_network'],
                      '--read-only', '--cap-drop=ALL', '--security-opt=no-new-privileges',
                      '--memory=128m', '--cpus=1', runner.image, 'node', '-e', probe, gateway], runner.root, 60)
    print(result, flush=True)
    class SmokeLease:
        lost = threading.Event()
        paused = False
    with tempfile.TemporaryDirectory(prefix='acceptance-', dir=runner.root) as directory:
        workspace = Path(directory)
        source = workspace / 'frontend/src/repair-smoke.cjs'
        source.parent.mkdir(parents=True)
        source.write_text('module.exports = (a, b) => a - b;\n')
        result = runner.container(workspace, {'instructions':
            'This is an isolated acceptance test. Fix frontend/src/repair-smoke.cjs so it adds its arguments. '
            'Change only that file and verify that 2 plus 3 is 5. Return the required JSON schema with '
            'a truthful summary, safeToDeploy=true only if verified, critical=false, reproduction and verification evidence.'}, SmokeLease())
        review = json.loads(result)
        if review.get('safeToDeploy') is not True:
            raise RuntimeError('Real Codex did not verify the disposable repair: ' + redact(json.dumps(review)))
        command([*DOCKER, 'run', '--rm', '--pull=never', '--network=none', '--read-only', '--cap-drop=ALL',
            '--user', f'{os.getuid()}:{os.getgid()}',
            '--security-opt=no-new-privileges', '--mount', f'type=bind,src={workspace},dst=/workspace,readonly',
            runner.image, 'node', '-e',
            "const add=require('/workspace/frontend/src/repair-smoke.cjs'); if(add(2,3)!==5 || add(-2,3)!==1) process.exit(1)"], runner.root)
    print('Real Codex repair passed independent execution check; no production source changed', flush=True)


if __name__ == '__main__':
    parser = argparse.ArgumentParser()
    mode = parser.add_mutually_exclusive_group(required=True)
    mode.add_argument('--dry-run', action='store_true')
    mode.add_argument('--yes', action='store_true')
    args = parser.parse_args()
    if args.dry_run:
        print('Probe network isolation; repair and independently execute a disposable addition function using real Codex. No deployment or production API mutation.')
    else:
        smoke(Runner(json.loads(Path('/etc/massar/auto-repair.json').read_text())))
