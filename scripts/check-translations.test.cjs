const { test } = require('node:test');
const assert = require('node:assert/strict');
const path = require('node:path');
const { placeholders, validate, checkRepository } = require('./check-translations.cjs');
test('translation catalogs and all UI resource references validate', () => {
  assert.deepEqual(checkRepository(path.resolve(__dirname, '..')).errors, []);
});
test('placeholder checks accept reordering and preserve number/date formatting', () => {
  assert.deepEqual(validate({ line: '{0}: {1:N0}' }, { line: '{1:N0}：{0}' }), []);
  assert.equal(validate({ line: '{0:N0}' }, { line: '{0}' }).length, 1);
  assert.equal(validate({ line: '{0}' }, { line: '{1}' }).length, 1);
  assert.equal(validate({ line: '{0}' }, { line: '{0' }).length, 1);
});
test('empty or missing translations are allowed; unknown keys and nested values are rejected', () => {
  assert.deepEqual(validate({ a: 'Source' }, {}), []);
  assert.deepEqual(validate({ a: 'Source' }, { a: '' }), []);
  assert.equal(validate({ a: 'Source' }, { extra: 'x' }).length, 1);
  assert.equal(validate({ a: 'Source' }, { a: { nested: 'x' } }).length, 1);
});
test('escaped braces remain literals', () => {
  assert.deepEqual(placeholders('{{example}} {0}'), ['0']);
  assert.throws(() => placeholders('broken }'));
});
