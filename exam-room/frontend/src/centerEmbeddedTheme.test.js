import test from 'node:test';
import assert from 'node:assert/strict';
import {installCenterEmbeddedTheme,resolveCenterTheme} from './centerEmbeddedTheme.js';

function surface(search) {
  const classes=new Set(), values=new Map();
  const root={classList:{add:value=>classes.add(value)},dataset:{},style:{setProperty:(key,value)=>values.set(key,value)}};
  return {window:{location:{search}},document:{documentElement:root,body:{classList:{add:value=>classes.add(value)}}},classes,values};
}

test('student and ordinary browser surfaces cannot receive an embedded theme bridge',()=>{
  for(const query of ['','?theme=dark','?embedded=student']) {
    const state=surface(query);
    assert.equal(installCenterEmbeddedTheme(state.window,state.document),false);
    assert.equal(state.window.applyMassarCenterTheme,undefined);
    assert.equal(state.classes.size,0);
  }
});

test('embedded surface applies native palette before rendering and accepts appearance changes',()=>{
  const state=surface('?embedded=center');
  state.window.__massarCenterTheme={mode:'dark',colors:{accent:'#ABCDEF',surface:'#182637'},onPrimary:'#092625'};
  assert.equal(installCenterEmbeddedTheme(state.window,state.document),true);
  assert.equal(state.values.get('--paper'),'#182637');
  assert.equal(state.values.get('--teal'),'#ABCDEF');
  assert.equal(state.values.get('--on-primary'),'#092625');
  assert.equal(state.document.documentElement.dataset.centerTheme,'dark');
  assert.equal(state.values.get('--navy'),'#E6EDF5');
  state.window.applyMassarCenterTheme({mode:'light'});
  assert.equal(state.values.get('--ink'),'#0A1D3D');
  assert.equal(state.document.documentElement.style.colorScheme,'light');
});

test('only known roles and literal hex colors cross the visual bridge',()=>{
  const theme=resolveCenterTheme({mode:'unexpected',colors:{ink:'url(https://invalid.test/x)',accent:'#123456',canvas:'red;display:none',unknown:'#FFFFFF'}});
  assert.equal(theme.mode,'light');
  assert.equal(theme.colors.ink,'#0A1D3D');
  assert.equal(theme.colors.canvas,'#F6F7F8');
  assert.equal(theme.colors.accent,'#123456');
  assert.equal(theme.colors.unknown,undefined);
});
