"""Run the actual installer against isolated files and fake macOS command boundaries."""
import json
import os
from pathlib import Path
import plistlib
import shlex
import shutil
import signal
import stat
import subprocess
import sys
import tempfile
import time
import unittest


FAKE_OS = r'''
import json, os, pathlib, plistlib, shutil, sys, time
name=pathlib.Path(sys.argv[1]).name
args=sys.argv[2:]
state_path=pathlib.Path(os.environ['INSTALL_TEST_STATE'])
state=json.loads(state_path.read_text())
state['calls'].append([name]+args)
def finish(code=0):
    state_path.write_text(json.dumps(state))
    sys.exit(code)
def fail_once(point):
    if state.get('fail')==point and not state.get('failedOnce'):
        state['failedOnce']=True
        finish(1)
if name=='id':
    print('0'); finish()
if name=='date':
    clock=state.get('clock', [int(time.time())])
    print(clock.pop(0) if len(clock)>1 else clock[0]); finish()
if name=='sleep': finish()
if name=='ifconfig':
    links=state.get('linkSequence', ['active'])
    link=links.pop(0) if len(links)>1 else links[0]
    if state.get('dropLinkAfterBootstrap') and state.get('bootstrapSeen'): link='inactive'
    print('status: '+link)
    waiting=state.get('addressDelay', 0) if state.get('manualApplied') else 0
    if waiting: state['addressDelay']=waiting-1
    if not waiting and not state.get('missingAddress'):
        lines=dict(line.split(': ',1) for line in state['interface'].splitlines() if ': ' in line)
        if 'IP address' in lines:
            print(' inet '+lines['IP address']+' netmask '+lines['Subnet mask'])
    finish()
if name=='networksetup':
    if args[0]=='-listnetworkserviceorder':
        print('(1) Ethernet\n(Hardware Port: Ethernet, Device: en9)')
    elif args[0]=='-getinfo': print(state['interface'])
    elif args[0]=='-setmanual':
        fail_once('manual_network')
        state['interface']='Manual Configuration\nIP address: '+args[2]+'\nSubnet mask: '+args[3]+'\nRouter: '+args[4]+'\n'
        state['manualApplied']=True
    elif args[0]=='-setdhcp': state['interface']='DHCP Configuration\nClient ID: '+('' if args[2]=='Empty' else args[2])+'\n'
    else: finish(90)
    finish()
if name=='lsof':
    endpoint=next(arg[2:] for arg in args if arg.startswith('-i')).removeprefix('4')
    port=endpoint.split(':')[1]
    default=[]
    dhcp_pid=state['loaded'].get('com.massar.examroom.dhcp')
    if dhcp_pid and endpoint in ('UDP:67','UDP:53','TCP:53'):
        default=[dhcp_pid]
    if endpoint=='UDP:67' and dhcp_pid==322 and state.get('missingDhcpSocket'):
        default=[]
    owners=state.get('ports',{}).get(endpoint,state.get('ports',{}).get(port,default))
    for owner in owners:
        if '-Fpn' in args: print('p'+str(owner)+'\nn*:'+port)
        else: print(owner)
    if '-Fpn' in args and state.get('outboundClients'):
        print('p777\nn192.168.1.5:55555->192.168.1.1:'+port)
    finish(0 if owners else 1)
if name=='launchctl':
    operation=args[0]
    if operation=='print-disabled':
        for label in state['disabled']: print('"'+label+'" => true')
        finish()
    if operation=='bootstrap':
        label=plistlib.loads(pathlib.Path(args[-1]).read_bytes())['Label']
        if label.endswith('portal'): fail_once('bootstrap_portal')
        state['loaded'][label]=321 if label.endswith('portal') else 322
        state['bootstrapSeen']=True
        finish()
    label=args[-1].removeprefix('system/')
    if operation=='print':
        if label not in state['loaded']: finish(1)
        print('state = running\npid = '+str(state['loaded'][label])); finish()
    if operation=='bootout':
        if label not in state['loaded']: finish(1)
        if state.get('pauseBootout'):
            (state_path.parent/'paused').touch()
            limit=time.monotonic()+5
            while not (state_path.parent/'release-command').exists() and time.monotonic()<limit:
                time.sleep(0.01)
        del state['loaded'][label]; finish()
    if operation=='enable':
        state['disabled']=[item for item in state['disabled'] if item!=label]; finish()
    if operation=='disable':
        if label not in state['disabled']: state['disabled'].append(label)
        finish()
    if operation=='kickstart':
        if label.endswith('dhcp'): fail_once('kickstart_dhcp')
        finish(0 if label in state['loaded'] else 1)
    finish(90)
if name=='plutil':
    if args[0]=='-extract': print(json.loads(pathlib.Path(args[-1]).read_text())[args[1]])
    elif args[0]=='-lint':
        for filename in args[1:]: plistlib.loads(pathlib.Path(filename).read_bytes())
    else: finish(90)
    finish()
if name=='install':
    files=[]; mode=0o755; directory=False; index=0
    while index<len(args):
        argument=args[index]
        if argument in ('-o','-g'): index+=2
        elif argument=='-m': mode=int(args[index+1],8); index+=2
        elif argument=='-d': directory=True; index+=1
        else: files.append(argument); index+=1
    if directory:
        pathlib.Path(files[0]).mkdir(parents=True,exist_ok=True); os.chmod(files[0],mode)
    else:
        if files[-1].endswith('/privkey.pem'): fail_once('install_privkey')
        if files[-1].endswith('/portal-mode.json'): fail_once('install_mode')
        shutil.copyfile(files[0],files[1]); os.chmod(files[1],mode)
    finish()
if name=='dnsmasq':
    config=pathlib.Path(next(arg.split('=',1)[1] for arg in args if arg.startswith('--conf-file='))).read_text()
    finish(0 if 'interface=en9' in config else 1)
if name=='massar-exam-portal-proxy':
    assert '--check-capport' in args
    chain=pathlib.Path(args[args.index('--capport-cert')+1]).read_text()
    key=pathlib.Path(args[args.index('--capport-key')+1]).read_text()
    if chain!='CERT_VALID' or key!='PRIVATE_SECRET':
        print('certificate expired or invalid',file=sys.stderr); finish(1)
    print(json.dumps({'hostname':args[args.index('--capport-hostname')+1].lower(),
                      'expiresAt':'2030-01-01T00:00:00Z','daysRemaining':365}))
    finish()
finish(90)
'''


class TransactionalNetworkInstallTest(unittest.TestCase):
    def setUp(self):
        self.directory = tempfile.TemporaryDirectory(prefix='exam-network-install-')
        self.addCleanup(self.directory.cleanup)
        self.root = Path(self.directory.name)
        self.bundle = self.root / 'bundle with spaces'
        self.bin = self.root / 'fake-bin'
        self.bundle.mkdir()
        self.bin.mkdir()
        (self.root / 'tmp').mkdir()
        (self.root / 'var/run').mkdir(parents=True)
        self.state_path = self.root / 'os-state.json'
        self.state = {'interface': 'DHCP Configuration\nClient ID: exam-client\n',
                      'loaded': {}, 'disabled': [], 'ports': {}, 'calls': []}
        self.write_state()
        implementation = self.bin / 'fake-os.py'
        implementation.write_text(FAKE_OS)
        dispatcher = self.bin / 'fake-os'
        dispatcher.write_text('#!/bin/sh\nexec ' + shlex.quote(sys.executable) + ' ' +
                              shlex.quote(str(implementation)) + ' "$0" "$@"\n')
        dispatcher.chmod(0o755)
        for command in ('id', 'date', 'ifconfig', 'networksetup', 'lsof', 'launchctl', 'plutil', 'install', 'sleep'):
            (self.bin / command).symlink_to(dispatcher)
        for command in ('dnsmasq', 'massar-exam-portal-proxy'):
            (self.bundle / command).symlink_to(dispatcher)
        source = Path(__file__).parent / 'packaging'
        script = (source / 'mac-direct-setup.sh').read_text()
        # Only the filesystem/OS boundary changes in the fixture. Production has
        # no environment-controlled root, test switch, or unprivileged bypass.
        for prefix in ('/usr/local', '/var/db', '/var/run', '/Library/LaunchDaemons'):
            script = script.replace(prefix, str(self.root / prefix.lstrip('/')))
        script = script.replace('PATH=/usr/bin:/bin:/usr/sbin:/sbin',
                                f'PATH={self.bin}:/usr/bin:/bin:/usr/sbin:/sbin')
        script = script.replace('STAGE=$(mktemp -d)',
                                f'STAGE=$(mktemp -d "{self.root}/tmp/recovery.XXXXXX")')
        self.script = self.bundle / 'mac-direct-setup.sh'
        self.script.write_text(script)
        shutil.copyfile(source / 'omada-direct-dnsmasq.conf', self.bundle / 'omada-direct-dnsmasq.conf')
        self.data = self.root / 'var/db/massar-exam-room'
        self.config = self.root / 'usr/local/etc/massar-exam-dnsmasq.conf'
        self.proxy_plist = self.root / 'Library/LaunchDaemons/com.massar.examroom.portal.plist'
        self.dhcp_plist = self.proxy_plist.with_name('com.massar.examroom.dhcp.plist')
        self.cert_source = self.root / 'selected-cert'
        self.cert_source.mkdir()
        (self.cert_source / 'fullchain.pem').write_text('CERT_VALID')
        (self.cert_source / 'privkey.pem').write_text('PRIVATE_SECRET')

    def write_state(self):
        self.state_path.write_text(json.dumps(self.state))

    def install_arguments(self, hostname='', certificate_directory='', deadline=None, mode=None):
        arguments = ['/bin/sh', str(self.script), 'Ethernet', 'en9', hostname,
                     str(certificate_directory), str(int(time.time()) + 600 if deadline is None else deadline)]
        return arguments + [mode] if mode is not None else arguments

    def install_environment(self):
        return dict(os.environ, INSTALL_TEST_STATE=str(self.state_path), TMPDIR=str(self.root / 'tmp'))

    def run_install(self, hostname='', certificate_directory='', deadline=None, mode=None):
        completed = subprocess.run(self.install_arguments(hostname, certificate_directory, deadline, mode),
                                   env=self.install_environment(), capture_output=True,
                                   text=True, timeout=20)
        self.state = json.loads(self.state_path.read_text())
        return completed

    def seed_existing_network(self):
        self.config.parent.mkdir(parents=True)
        self.config.write_text('old network config\n')
        self.proxy_plist.parent.mkdir(parents=True)
        for plist, label in [(self.dhcp_plist, 'com.massar.examroom.dhcp'),
                             (self.proxy_plist, 'com.massar.examroom.portal')]:
            plist.write_bytes(plistlib.dumps({'Label': label, 'RunAtLoad': True}))
        library = self.root / 'usr/local/libexec'
        library.mkdir(parents=True)
        (library / 'massar-exam-dnsmasq').write_text('old dns binary')
        (library / 'massar-exam-portal-proxy').write_text('old proxy binary')
        self.data.mkdir(parents=True)
        (self.data / 'dnsmasq.log').write_text('existing diagnostics\n')
        self.state['loaded'] = {'com.massar.examroom.dhcp': 100, 'com.massar.examroom.portal': 123}
        self.state['ports'] = {'80': [123], '443': [123]}
        self.write_state()

    def test_new_capport_is_validated_then_installed_with_private_keys_and_preserved_logs(self):
        self.seed_existing_network()
        completed = self.run_install('Exam.Example.com', self.cert_source)
        self.assertEqual(completed.returncode, 0, completed.stderr)
        self.assertIn('dhcp-option-force=114,https://exam.example.com/capport', self.config.read_text())
        arguments = plistlib.loads(self.proxy_plist.read_bytes())['ProgramArguments']
        self.assertIn('--capport-hostname', arguments)
        self.assertIn(str(self.data / 'capport/privkey.pem'), arguments)
        self.assertEqual(stat.S_IMODE((self.data / 'capport/privkey.pem').stat().st_mode), 0o600)
        self.assertEqual(stat.S_IMODE((self.data / 'capport/fullchain.pem').stat().st_mode), 0o600)
        self.assertEqual(stat.S_IMODE((self.data / 'capport-public.json').stat().st_mode), 0o644)
        public = json.loads((self.data / 'capport-public.json').read_text())
        self.assertEqual(set(public), {'hostname', 'expiresAt', 'daysRemaining'})
        self.assertNotIn('PRIVATE_SECRET', json.dumps(public) + completed.stdout + completed.stderr)
        self.assertEqual((self.data / 'dnsmasq.log').read_text(), 'existing diagnostics\n')

    def test_existing_capport_is_preserved_without_new_certificate_selection(self):
        self.assertEqual(self.run_install('exam.example.com', self.cert_source).returncode, 0)
        completed = self.run_install()
        self.assertEqual(completed.returncode, 0, completed.stderr)
        self.assertIn('dhcp-option-force=114,https://exam.example.com/capport', self.config.read_text())
        self.assertEqual((self.data / 'capport/privkey.pem').read_text(), 'PRIVATE_SECRET')

    def test_never_configured_legacy_network_needs_no_certificate(self):
        completed = self.run_install()
        self.assertEqual(completed.returncode, 0, completed.stderr)
        self.assertNotIn('dhcp-option-force=114', self.config.read_text())
        self.assertFalse((self.data / 'capport-public.json').exists())
        for directory in (self.data, self.config.parent, self.root / 'usr/local/libexec'):
            self.assertEqual(stat.S_IMODE(directory.stat().st_mode), 0o755)

    def test_https_port_conflict_is_rejected_even_when_prior_proxy_plist_exists(self):
        self.seed_existing_network()
        self.state['ports']['443'] = [999]
        self.write_state()
        completed = self.run_install('exam.example.com', self.cert_source, mode='https')
        self.assertNotEqual(completed.returncode, 0)
        self.assertIn('443', completed.stderr)
        self.assertEqual(self.config.read_text(), 'old network config\n')
        self.assertEqual(self.state['loaded']['com.massar.examroom.portal'], 123)
        self.assertFalse(any(call[0] == 'install' or call[:2] == ['launchctl', 'bootout']
                             for call in self.state['calls']))

    def test_dns_and_dhcp_conflicts_fail_before_mutating_a_working_network(self):
        self.seed_existing_network()
        for endpoint in ('UDP:67', 'UDP:53', 'TCP:53'):
            with self.subTest(endpoint=endpoint):
                self.state['ports'] = {'80': [123], endpoint: [999]}
                self.state['calls'] = []
                self.write_state()
                completed = self.run_install(mode='legacy')
                self.assertNotEqual(completed.returncode, 0)
                self.assertIn(endpoint, completed.stderr)
                self.assertEqual(self.config.read_text(), 'old network config\n')
                self.assertFalse(any(call[0] == 'install' or call[:2] == ['launchctl', 'bootout']
                                     for call in self.state['calls']))

    def test_outbound_dns_or_dhcp_client_connections_do_not_block_setup(self):
        self.state['outboundClients'] = True
        self.write_state()
        completed = self.run_install(mode='legacy')
        self.assertEqual(completed.returncode, 0, completed.stderr)

    def test_running_process_without_dhcp_listener_rolls_back_instead_of_committing(self):
        self.seed_existing_network()
        self.state['missingDhcpSocket'] = True
        self.write_state()
        completed = self.run_install(mode='legacy')
        self.assertNotEqual(completed.returncode, 0)
        self.assertIn('DHCP', completed.stderr)
        self.assertEqual(self.config.read_text(), 'old network config\n')
        self.assertFalse((self.data / 'portal-mode.json').exists())
        self.assertFalse((self.root / 'var/run/massar-exam-network-setup.lock').exists(), completed.stderr)

    def test_hub_disconnect_during_preflight_preserves_working_network(self):
        self.seed_existing_network()
        self.state['linkSequence'] = ['active', 'inactive']
        self.write_state()
        completed = self.run_install(mode='legacy')
        self.assertNotEqual(completed.returncode, 0)
        self.assertEqual(self.config.read_text(), 'old network config\n')
        self.assertFalse(any(call[0] == 'install' or call[:2] == ['launchctl', 'bootout']
                             for call in self.state['calls']))
        self.assertFalse((self.root / 'var/run/massar-exam-network-setup.lock').exists())

    def test_changed_adapter_waits_for_actual_ip_before_starting_dhcp(self):
        self.seed_existing_network()
        self.config.write_text('interface=en8\n')
        self.state['addressDelay'] = 2
        self.write_state()
        completed = self.run_install(mode='legacy')
        self.assertEqual(completed.returncode, 0, completed.stderr)
        self.assertIn('interface=en9', self.config.read_text())
        calls = self.state['calls']
        set_ip = next(index for index, call in enumerate(calls) if call[:2] == ['networksetup', '-setmanual'])
        start_dhcp = next(index for index, call in enumerate(calls) if call[:2] == ['launchctl', 'bootstrap'])
        self.assertEqual(sum(call[0] == 'ifconfig' for call in calls[set_ip:start_dhcp]), 3)
        self.assertEqual(self.state['interface'], 'Manual Configuration\nIP address: 10.77.0.1\nSubnet mask: 255.255.252.0\nRouter: 0.0.0.0\n')

    def test_missing_runtime_address_and_link_loss_roll_back_successful_os_commands(self):
        self.seed_existing_network()
        original_interface = self.state['interface']
        for symptom in ('missingAddress', 'dropLinkAfterBootstrap'):
            with self.subTest(symptom=symptom):
                self.state[symptom] = True
                self.state['bootstrapSeen'] = False
                self.write_state()
                completed = self.run_install(mode='legacy')
                self.assertNotEqual(completed.returncode, 0)
                self.assertIn('تمت إعادة', completed.stderr)
                self.assertEqual(self.config.read_text(), 'old network config\n')
                self.assertEqual(self.state['interface'], original_interface)
                self.assertFalse((self.data / 'portal-mode.json').exists())
                self.assertFalse((self.root / 'var/run/massar-exam-network-setup.lock').exists())
                self.state.pop(symptom)
                self.state['ports'] = {'80': [self.state['loaded']['com.massar.examroom.portal']]}

    def test_invalid_selected_or_expired_installed_certificate_fails_without_mutation(self):
        self.seed_existing_network()
        (self.cert_source / 'fullchain.pem').write_text('EXPIRED')
        completed = self.run_install('exam.example.com', self.cert_source)
        self.assertNotEqual(completed.returncode, 0)
        self.assertEqual(self.config.read_text(), 'old network config\n')
        self.assertEqual(self.state['loaded']['com.massar.examroom.portal'], 123)
        (self.cert_source / 'fullchain.pem').write_text('CERT_VALID')
        self.assertEqual(self.run_install('exam.example.com', self.cert_source).returncode, 0)
        previous_config = self.config.read_bytes()
        (self.data / 'capport/fullchain.pem').write_text('EXPIRED')
        completed = self.run_install()
        self.assertNotEqual(completed.returncode, 0)
        self.assertEqual(self.config.read_bytes(), previous_config)
        self.assertEqual((self.data / 'capport/fullchain.pem').read_text(), 'EXPIRED')

    def test_invalid_hostname_or_relative_certificate_folder_is_rejected_before_mutation(self):
        for hostname, directory in [('exam.<bad>.com', self.cert_source), ('exam.example.com', 'relative')]:
            with self.subTest(hostname=hostname, directory=directory):
                completed = self.run_install(hostname, directory)
                self.assertNotEqual(completed.returncode, 0)
                self.assertFalse(self.config.exists())
                self.assertFalse(any(call[0] == 'install' for call in self.state['calls']))

    def test_mid_install_failures_restore_files_services_interface_and_logs(self):
        self.seed_existing_network()
        self.state['interface'] = 'Manual Configuration\nIP address: 10.77.0.1\nSubnet mask: 255.255.255.0\nRouter: 10.77.0.254\n'
        self.state['disabled'] = ['com.massar.examroom.dhcp']
        original_interface = self.state['interface']
        originals = {path: path.read_bytes() for folder in ('usr/local', 'Library/LaunchDaemons', 'var/db')
                     for path in (self.root / folder).rglob('*') if path.is_file()}
        for point in ('install_privkey', 'manual_network', 'bootstrap_portal', 'kickstart_dhcp'):
            with self.subTest(point=point):
                self.state['fail'] = point
                self.state['failedOnce'] = False
                self.write_state()
                completed = self.run_install('exam.example.com', self.cert_source)
                self.assertNotEqual(completed.returncode, 0)
                self.assertIn('تمت إعادة', completed.stderr)
                for path, contents in originals.items():
                    self.assertEqual(path.read_bytes(), contents, str(path))
                self.assertEqual(self.state['interface'], original_interface)
                self.assertEqual(set(self.state['loaded']), {'com.massar.examroom.dhcp', 'com.massar.examroom.portal'})
                self.assertEqual(self.state['disabled'], ['com.massar.examroom.dhcp'])
                self.assertFalse((self.data / 'capport').exists())
                self.assertFalse((self.data / 'capport-public.json').exists())
                # Fake lsof follows the restarted proxy for the next attempt.
                self.state['ports'] = {'80': [self.state['loaded']['com.massar.examroom.portal']]}

    def test_failed_first_install_restores_dhcp_and_removes_new_service_files(self):
        self.state['fail'] = 'bootstrap_portal'
        self.write_state()
        completed = self.run_install('exam.example.com', self.cert_source)
        self.assertNotEqual(completed.returncode, 0)
        self.assertEqual(self.state['interface'], 'DHCP Configuration\nClient ID: exam-client\n')
        self.assertEqual(self.state['loaded'], {})
        self.assertFalse(self.config.exists())
        self.assertFalse(self.proxy_plist.exists())
        self.assertFalse((self.data / 'capport').exists())

    def test_rollback_clears_an_originally_empty_dhcp_client_id(self):
        self.state['interface'] = 'DHCP Configuration\nClient ID: \n'
        self.state['fail'] = 'bootstrap_portal'
        self.write_state()
        completed = self.run_install()
        self.assertNotEqual(completed.returncode, 0)
        self.assertEqual(self.state['interface'], 'DHCP Configuration\nClient ID: \n')

    def test_failed_capport_update_restores_previous_hostname_metadata_and_certificates(self):
        self.assertEqual(self.run_install('old.example.com', self.cert_source).returncode, 0)
        originals = {path: path.read_bytes() for path in (self.config, self.proxy_plist,
                     self.data / 'capport-public.json', self.data / 'capport/fullchain.pem',
                     self.data / 'capport/privkey.pem')}
        self.state['fail'] = 'bootstrap_portal'
        self.write_state()
        completed = self.run_install('new.example.com', self.cert_source)
        self.assertNotEqual(completed.returncode, 0)
        for path, contents in originals.items():
            self.assertEqual(path.read_bytes(), contents, str(path))
        self.assertEqual(stat.S_IMODE((self.data / 'capport/privkey.pem').stat().st_mode), 0o600)

    def test_second_installer_does_not_touch_a_running_transaction(self):
        lock = self.root / 'var/run/massar-exam-network-setup.lock'
        lock.mkdir()
        completed = self.run_install()
        self.assertNotEqual(completed.returncode, 0)
        self.assertFalse(self.config.exists())
        self.assertTrue(lock.is_dir())

    def test_switching_https_to_legacy_and_back_preserves_certificate_and_explicit_mode(self):
        self.assertEqual(self.run_install('exam.example.com', self.cert_source, mode='https').returncode, 0)
        originals = {path: path.read_bytes() for path in (self.data / 'capport-public.json',
                     self.data / 'capport/fullchain.pem', self.data / 'capport/privkey.pem')}
        self.state['ports'] = {'443': [999]}
        self.write_state()
        # Legacy must be usable even while another program owns port 443.
        for mode in ('legacy', None):
            completed = self.run_install(mode=mode)
            self.assertEqual(completed.returncode, 0, completed.stderr)
            self.assertEqual(json.loads((self.data / 'portal-mode.json').read_text()), {'mode': 'legacy'})
            self.assertNotIn('dhcp-option-force=114', self.config.read_text())
            self.assertNotIn('--capport-hostname', plistlib.loads(self.proxy_plist.read_bytes())['ProgramArguments'])
            for path, contents in originals.items():
                self.assertEqual(path.read_bytes(), contents)
        self.state['ports'] = {}
        self.write_state()
        completed = self.run_install(mode='https')
        self.assertEqual(completed.returncode, 0, completed.stderr)
        self.assertEqual(json.loads((self.data / 'portal-mode.json').read_text()), {'mode': 'https'})
        self.assertEqual(stat.S_IMODE((self.data / 'portal-mode.json').stat().st_mode), 0o644)
        self.assertIn('dhcp-option-force=114,https://exam.example.com/capport', self.config.read_text())
        self.assertIn('--capport-hostname', plistlib.loads(self.proxy_plist.read_bytes())['ProgramArguments'])

    def test_explicit_legacy_works_with_expired_certificate_and_corrupt_metadata(self):
        self.assertEqual(self.run_install('exam.example.com', self.cert_source, mode='https').returncode, 0)
        (self.data / 'capport/fullchain.pem').write_text('EXPIRED')
        (self.data / 'capport-public.json').write_text('invalid metadata')
        (self.data / 'portal-mode.json').write_text('invalid mode')
        completed = self.run_install('ignored.invalid', 'ignored-relative-path', mode='legacy')
        self.assertEqual(completed.returncode, 0, completed.stderr)
        self.assertEqual(json.loads((self.data / 'portal-mode.json').read_text()), {'mode': 'legacy'})
        self.assertEqual((self.data / 'capport/fullchain.pem').read_text(), 'EXPIRED')
        self.assertEqual((self.data / 'capport-public.json').read_text(), 'invalid metadata')
        self.assertNotIn('dhcp-option-force=114', self.config.read_text())

    def test_https_without_certificate_leaves_legacy_network_untouched(self):
        self.assertEqual(self.run_install(mode='legacy').returncode, 0)
        originals = {path: path.read_bytes() for path in (self.config, self.proxy_plist,
                     self.data / 'portal-mode.json')}
        completed = self.run_install(mode='https')
        self.assertNotEqual(completed.returncode, 0)
        self.assertIn('يحتاج شهادة', completed.stderr)
        for path, contents in originals.items():
            self.assertEqual(path.read_bytes(), contents)

    def test_failed_mode_switch_restores_prior_mode_services_and_certificate(self):
        self.assertEqual(self.run_install('exam.example.com', self.cert_source, mode='https').returncode, 0)
        for old_mode, next_mode, point in [('https', 'legacy', 'install_mode'),
                                           ('legacy', 'https', 'bootstrap_portal')]:
            with self.subTest(old_mode=old_mode, next_mode=next_mode, point=point):
                self.assertEqual(self.run_install(mode=old_mode).returncode, 0)
                originals = {path: path.read_bytes() for path in (self.config, self.proxy_plist,
                             self.data / 'portal-mode.json', self.data / 'capport-public.json',
                             self.data / 'capport/fullchain.pem', self.data / 'capport/privkey.pem')}
                self.state['fail'] = point
                self.state['failedOnce'] = False
                self.write_state()
                completed = self.run_install(mode=next_mode)
                self.assertNotEqual(completed.returncode, 0)
                self.assertIn('تمت إعادة', completed.stderr)
                for path, contents in originals.items():
                    self.assertEqual(path.read_bytes(), contents)
                self.assertEqual(set(self.state['loaded']), {'com.massar.examroom.dhcp', 'com.massar.examroom.portal'})
                self.assertFalse((self.root / 'var/run/massar-exam-network-setup.lock').exists())

    def test_old_https_install_without_mode_file_remains_https(self):
        self.assertEqual(self.run_install('exam.example.com', self.cert_source, mode='https').returncode, 0)
        (self.data / 'portal-mode.json').unlink()
        completed = self.run_install()
        self.assertEqual(completed.returncode, 0, completed.stderr)
        self.assertEqual(json.loads((self.data / 'portal-mode.json').read_text()), {'mode': 'https'})
        self.assertIn('dhcp-option-force=114', self.config.read_text())

    def test_expired_authorization_before_start_or_after_preflight_changes_no_network(self):
        self.seed_existing_network()
        for clock in ([1700000601], [1700000000, 1700000601]):
            with self.subTest(clock=clock):
                self.state['clock'] = clock
                self.write_state()
                completed = self.run_install(deadline=1700000600)
                self.assertNotEqual(completed.returncode, 0)
                self.assertIn('انتهى تصريح', completed.stderr)
                self.assertEqual(self.config.read_text(), 'old network config\n')
                self.assertEqual(self.state['loaded']['com.massar.examroom.portal'], 123)
                self.assertFalse((self.root / 'var/run/massar-exam-network-setup.lock').exists())
                self.assertFalse(any(call[0] == 'install' or call[:2] == ['launchctl', 'bootout']
                                     for call in self.state['calls']))

    def test_interrupted_install_preserves_lock_and_recovery_files(self):
        self.seed_existing_network()
        self.state['pauseBootout'] = True
        self.write_state()
        process = subprocess.Popen(self.install_arguments(), env=self.install_environment(),
                                   stdout=subprocess.PIPE, stderr=subprocess.PIPE, text=True,
                                   start_new_session=True)
        try:
            limit = time.monotonic() + 5
            while not (self.root / 'paused').exists() and time.monotonic() < limit:
                if process.poll() is not None:
                    self.fail('Installer exited before reaching the interrupted command')
                time.sleep(0.01)
            self.assertTrue((self.root / 'paused').exists())
            process.send_signal(signal.SIGTERM)
            (self.root / 'release-command').touch()
            _, stderr = process.communicate(timeout=5)
            self.assertNotEqual(process.returncode, 0)
            self.assertIn('أعد تشغيل الماك', stderr)
            self.assertTrue((self.root / 'var/run/massar-exam-network-setup.lock').is_dir())
            self.assertTrue(list((self.root / 'tmp').glob('*/old/massar-exam-dnsmasq.conf')))
            state = json.loads(self.state_path.read_text())
            self.assertFalse(any(call[:2] == ['launchctl', 'bootstrap'] for call in state['calls']))
        finally:
            if process.poll() is None:
                try:
                    os.killpg(process.pid, signal.SIGKILL)
                except ProcessLookupError:
                    pass
            # Even an early assertion after process exit must drain/close pipes.
            process.communicate()


if __name__ == '__main__':
    unittest.main()
