import assert from 'node:assert/strict';
import test from 'node:test';
import {centerRoomSelection} from './centerRoomSelection.js';

test('native-created waiting room appears when embedded admin mounted with no rooms', () => {
  assert.equal(centerRoomSelection([], null), null);
  assert.equal(centerRoomSelection([{id:'new',state:'waiting',createdAt:20}], null), 'new');
});

test('new native room replaces an older closed selection while preserving grading', () => {
  const rooms=[{id:'new',state:'waiting',createdAt:20},{id:'old',state:'closed',createdAt:10}];
  assert.equal(centerRoomSelection(rooms,'old'), 'new');
  assert.equal(centerRoomSelection(rooms,'old',true), null);
  assert.equal(centerRoomSelection(rooms,'new'), null);
});

test('polling preserves current active/draft room and deliberate older history', () => {
  for(const state of ['draft','waiting','running']) {
    assert.equal(centerRoomSelection([{id:'other',state:'waiting',createdAt:20},{id:'current',state,createdAt:10}],'current'), null);
  }
  assert.equal(centerRoomSelection([{id:'older',state:'waiting',createdAt:5},{id:'closed',state:'closed',createdAt:10}],'closed'), null);
});
