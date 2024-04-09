/*!
  * Bootstrap Autocomplete v0.2.0 (https://iqbalfn.github.io/bootstrap-autocomplete/)
  * Copyright 2019 Iqbal Fauzi
  * Licensed under MIT (https://github.com/iqbalfn/bootstrap-autocomplete/blob/master/LICENSE)
  */
(function (global, factory) {
  typeof exports === 'object' && typeof module !== 'undefined' ? factory(exports, require('jquery')) :
  typeof define === 'function' && define.amd ? define(['exports', 'jquery'], factory) :
  (global = global || self, factory(global['bootstrap-autocomplete'] = {}, global.jQuery));
}(this, (function (exports, $) { 'use strict';

  $ = $ && Object.prototype.hasOwnProperty.call($, 'default') ? $['default'] : $;

  function _defineProperties(target, props) {
    for (var i = 0; i < props.length; i++) {
      var descriptor = props[i];
      descriptor.enumerable = descriptor.enumerable || false;
      descriptor.configurable = true;
      if ("value" in descriptor) descriptor.writable = true;
      Object.defineProperty(target, descriptor.key, descriptor);
    }
  }

  function _createClass(Constructor, protoProps, staticProps) {
    if (protoProps) _defineProperties(Constructor.prototype, protoProps);
    if (staticProps) _defineProperties(Constructor, staticProps);
    return Constructor;
  }

  function _defineProperty(obj, key, value) {
    if (key in obj) {
      Object.defineProperty(obj, key, {
        value: value,
        enumerable: true,
        configurable: true,
        writable: true
      });
    } else {
      obj[key] = value;
    }

    return obj;
  }

  function _objectSpread(target) {
    for (var i = 1; i < arguments.length; i++) {
      var source = arguments[i] != null ? Object(arguments[i]) : {};
      var ownKeys = Object.keys(source);

      if (typeof Object.getOwnPropertySymbols === 'function') {
        ownKeys = ownKeys.concat(Object.getOwnPropertySymbols(source).filter(function (sym) {
          return Object.getOwnPropertyDescriptor(source, sym).enumerable;
        }));
      }

      ownKeys.forEach(function (key) {
        _defineProperty(target, key, source[key]);
      });
    }

    return target;
  }

  /**
   * --------------------------------------------------------------------------
   * Bootstrap (v4.3.1): util.js
   * Licensed under MIT (https://github.com/twbs/bootstrap/blob/master/LICENSE)
   * --------------------------------------------------------------------------
   */
  /**
   * ------------------------------------------------------------------------
   * Private TransitionEnd Helpers
   * ------------------------------------------------------------------------
   */

  var TRANSITION_END = 'transitionend';
  var MAX_UID = 1000000;
  var MILLISECONDS_MULTIPLIER = 1000; // Shoutout AngusCroll (https://goo.gl/pxwQGp)

  function toType(obj) {
    return {}.toString.call(obj).match(/\s([a-z]+)/i)[1].toLowerCase();
  }

  function getSpecialTransitionEndEvent() {
    return {
      bindType: TRANSITION_END,
      delegateType: TRANSITION_END,
      handle: function handle(event) {
        if ($(event.target).is(this)) {
          return event.handleObj.handler.apply(this, arguments); // eslint-disable-line prefer-rest-params
        }

        return undefined; // eslint-disable-line no-undefined
      }
    };
  }

  function transitionEndEmulator(duration) {
    var _this = this;

    var called = false;
    $(this).one(Util.TRANSITION_END, function () {
      called = true;
    });
    setTimeout(function () {
      if (!called) {
        Util.triggerTransitionEnd(_this);
      }
    }, duration);
    return this;
  }

  function setTransitionEndSupport() {
    $.fn.emulateTransitionEnd = transitionEndEmulator;
    $.event.special[Util.TRANSITION_END] = getSpecialTransitionEndEvent();
  }
  /**
   * --------------------------------------------------------------------------
   * Public Util Api
   * --------------------------------------------------------------------------
   */


  var Util = {
    TRANSITION_END: 'bsTransitionEnd',
    getUID: function getUID(prefix) {
      do {
        // eslint-disable-next-line no-bitwise
        prefix += ~~(Math.random() * MAX_UID); // "~~" acts like a faster Math.floor() here
      } while (document.getElementById(prefix));

      return prefix;
    },
    getSelectorFromElement: function getSelectorFromElement(element) {
      var selector = element.getAttribute('data-target');

      if (!selector || selector === '#') {
        var hrefAttr = element.getAttribute('href');
        selector = hrefAttr && hrefAttr !== '#' ? hrefAttr.trim() : '';
      }

      try {
        return document.querySelector(selector) ? selector : null;
      } catch (err) {
        return null;
      }
    },
    getTransitionDurationFromElement: function getTransitionDurationFromElement(element) {
      if (!element) {
        return 0;
      } // Get transition-duration of the element


      var transitionDuration = $(element).css('transition-duration');
      var transitionDelay = $(element).css('transition-delay');
      var floatTransitionDuration = parseFloat(transitionDuration);
      var floatTransitionDelay = parseFloat(transitionDelay); // Return 0 if element or transition duration is not found

      if (!floatTransitionDuration && !floatTransitionDelay) {
        return 0;
      } // If multiple durations are defined, take the first


      transitionDuration = transitionDuration.split(',')[0];
      transitionDelay = transitionDelay.split(',')[0];
      return (parseFloat(transitionDuration) + parseFloat(transitionDelay)) * MILLISECONDS_MULTIPLIER;
    },
    reflow: function reflow(element) {
      return element.offsetHeight;
    },
    triggerTransitionEnd: function triggerTransitionEnd(element) {
      $(element).trigger(TRANSITION_END);
    },
    // TODO: Remove in v5
    supportsTransitionEnd: function supportsTransitionEnd() {
      return Boolean(TRANSITION_END);
    },
    isElement: function isElement(obj) {
      return (obj[0] || obj).nodeType;
    },
    typeCheckConfig: function typeCheckConfig(componentName, config, configTypes) {
      for (var property in configTypes) {
        if (Object.prototype.hasOwnProperty.call(configTypes, property)) {
          var expectedTypes = configTypes[property];
          var value = config[property];
          var valueType = value && Util.isElement(value) ? 'element' : toType(value);

          if (!new RegExp(expectedTypes).test(valueType)) {
            throw new Error(componentName.toUpperCase() + ": " + ("Option \"" + property + "\" provided type \"" + valueType + "\" ") + ("but expected type \"" + expectedTypes + "\"."));
          }
        }
      }
    },
    findShadowRoot: function findShadowRoot(element) {
      if (!document.documentElement.attachShadow) {
        return null;
      } // Can find the shadow root otherwise it'll return the document


      if (typeof element.getRootNode === 'function') {
        var root = element.getRootNode();
        return root instanceof ShadowRoot ? root : null;
      }

      if (element instanceof ShadowRoot) {
        return element;
      } // when we don't find a shadow root


      if (!element.parentNode) {
        return null;
      }

      return Util.findShadowRoot(element.parentNode);
    }
  };
  setTransitionEndSupport();

  /**
   * ------------------------------------------------------------------------
   * Constants
   * ------------------------------------------------------------------------
   */

  var NAME = 'autocomplete';
  var VERSION = '0.2.0';
  var DATA_KEY = 'bs.autocomplete';
  var EVENT_KEY = "." + DATA_KEY;
  var DATA_API_KEY = '.data-api';
  var JQUERY_NO_CONFLICT = $.fn[NAME];
  var Default = {
    list: null,
    prefetch: null,
    filter: null,
    filterDelay: 300,
    filterMinChars: 1,
    filterRelation: null,
    maxResult: 10,
    onPick: null,
    onItemRendered: null,
    preProcess: null
  };
  var DefaultType = {
    list: '(null|string|element)',
    prefetch: '(null|string)',
    filter: '(null|string)',
    filterDelay: 'number',
    filterMinChars: 'number',
    filterRelation: '(null|object)',
    maxResult: 'number',
    preProcess: '(null|function)',
    onPick: '(null|function)',
    onItemRendered: '(null|function)'
  };
  var Event = {
    BLUR_DATA_API: "blur" + EVENT_KEY + DATA_API_KEY,
    CLICK_DATA_API: "click" + EVENT_KEY + DATA_API_KEY,
    KEYDOWN_DATA_API: "keydown" + EVENT_KEY + DATA_API_KEY,
    INPUT_DATA_API: "input" + EVENT_KEY + DATA_API_KEY,
    PICK: "pick" + EVENT_KEY,
    ITEM_RENDER: "itemrender" + EVENT_KEY
  };
  var KeyCode = {
    ARROW_DOWN: 40,
    ARROW_UP: 38,
    ENTER: 13,
    ESCAPE: 27
  };
  /**
   * ------------------------------------------------------------------------
   * Class Definition
   * ------------------------------------------------------------------------
   */

  var Autocomplete = /*#__PURE__*/function () {
    function Autocomplete(element, config) {
      this._config = this._getConfig(config);
      this._element = element;
      this._items = [];
      this._labels = {};
      this._isShown = false;
      this._dropdown = null;
      this._spinner = null;
      this._result = [];
      this._query = '';
      this._preventClose = false;
      this._timer = null;
      this._relations = null;

      if (element.hasAttribute('list')) {
        this._config.list = '#' + element.getAttribute('list');
        element.removeAttribute('list');
      }

      if (!this._config.list && !this._config.prefetch && !this._config.filter) throw new TypeError('No data source provided');
      if (this._config.filterRelation) this._handleRelations();
      element.setAttribute('autocomplete', 'off');

      this._makeDropdown();

      this._makeSpinner();

      this._fetchPresetData();

      this._addElementListener();
    } // Getters


    var _proto = Autocomplete.prototype;

    // Public
    _proto.show = function show() {
      if (this._isShown) return;
      if (this._result.length) this._showDropdown();
    };

    _proto.hide = function hide() {
      if (!this._isShown) return;

      this._hideDropdown();
    };

    _proto.dispose = function dispose() {
      $(this._element).off(EVENT_KEY);
      $.removeData(this._element, DATA_KEY);
      this._config = null;
      this._element = null;
      this._items = null;
      this._labels = null;
      this._isShown = null;
      this._dropdown = null;
      this._spinner = null;
      this._result = null;
      this._query = null;
      this._preventClose = null;
      if (this._timer) clearTimeout(this._timer);
      this._timer = null;
    } // Private
    ;

    _proto._addElementListener = function _addElementListener() {
      var _this = this;

      $(this._element).on(Event.KEYDOWN_DATA_API, function (e) {
        var prevent = false;

        switch (e.keyCode) {
          case KeyCode.ARROW_DOWN:
            if (e.altKey && !_this._isShown) {
              _this._query = _this._element.value.trim().toLowerCase();

              if (_this._query) {
                _this._findFromList();

                prevent = true;
              }
            } else {
              _this._focusNext();

              prevent = true;
            }

            break;

          case KeyCode.ARROW_UP:
            _this._focusPrev();

            prevent = true;
            break;

          case KeyCode.ENTER:
            if (_this._isShown) {
              var item = $(_this._dropdown).children('.active').get(0);
              if (!item) item = $(_this._dropdown).children(':first-child').get(0);
              if (item) _this._selectItem(item);

              _this.hide();

              prevent = true;
            }

            break;

          case KeyCode.ESCAPE:
            prevent = true;

            _this.hide();

            break;
        }

        if (prevent) e.preventDefault();
      });
      $(this._element).on(Event.INPUT_DATA_API, function (e) {
        _this._query = _this._element.value.trim().toLowerCase();

        if (!_this._query) {
          _this._truncateDropdown();

          _this.hide();
        } else {
          _this._findFromList();
        }
      });
      $(this._element).on(Event.BLUR_DATA_API, function (e) {
        if (_this._preventClose) return;
        setTimeout(function () {
          return _this.hide();
        }, 150);
      });
    };

    _proto._fetchPresetData = function _fetchPresetData() {
      var _this2 = this;

      // from datalist
      if (this._config.list) {
        var dataList = this._config.list;
        if (typeof dataList === 'string') dataList = document.querySelector(this._config.list);

        if (dataList) {
          Array.from(dataList.children).forEach(function (o) {
            var val = o.innerHTML.toLowerCase();
            if (_this2._items.includes(val)) return;

            _this2._items.push(val);

            _this2._labels[val] = o.innerHTML;
          });
        }
      } // from prefetch


      if (this._config.prefetch) {
        this._showSpinner();

        $.get(this._config.prefetch, function (res) {
          _this2._hideSpinner();

          if (_this2._config.preProcess) res = _this2._config.preProcess(res);
          res.forEach(function (i) {
            var val = i.toLowerCase();
            if (_this2._items.includes(val)) return;

            _this2._items.push(val);

            _this2._labels[val] = i;
          });
        });
      }
    };

    _proto._findFromAjax = function _findFromAjax() {
      var _this3 = this;

      if (!this._config.filter) return;
      if (this._dropdown.children.length >= this._config.maxResult) return;
      if (this._timer) clearTimeout(this._timer);
      var vval = this._query;
      this._timer = setTimeout(function () {
        if (vval != _this3._query) return;

        _this3._showSpinner();

        var url = _this3._config.filter.replace(/%23/g, '#').replace('#QUERY#', _this3._query);

        if (_this3._relations) {
          var sep = url.includes('?') ? '&' : '?';

          for (var k in _this3._relations) {
            var el = _this3._relations[k];
            var val = $(el).val();
            if (!val) continue;
            url += "" + sep + k + "=" + val;
            sep = '&';
          }
        }

        $.get(url, function (res) {
          _this3._hideSpinner();

          if (_this3._config.preProcess) res = _this3._config.preProcess(res);
          var local = [];
          res.forEach(function (i) {
            var val = i.toLowerCase();
            if (_this3._items.includes(val)) return;

            _this3._items.push(val);

            _this3._labels[val] = i;
            local.push(i);

            _this3._result.push(i);
          });
          if (local.length) _this3._renderItem(local);
          if (_this3._result.length) _this3.show();else _this3.hide();
        });
      }, this._config.filterDelay);
    };

    _proto._findFromList = function _findFromList() {
      var _this4 = this;

      this._truncateDropdown();

      this._result = [];
      var local = [];

      this._items.forEach(function (i) {
        if (!i.includes(_this4._query)) return;
        var label = _this4._labels[i];
        if (_this4._result.includes(label)) return;
        local.push(label);

        _this4._result.push(label);
      }); // now render the result


      if (local.length) {
        this._renderItem(local);

        this.show();
      } else {
        this.hide();
      }

      this._findFromAjax();
    };

    _proto._focusNext = function _focusNext() {
      var next = $(this._dropdown).children(':first-child').get(0);
      var focused = $(this._dropdown).children('.active').get(0);

      if (focused) {
        focused.classList.remove('active');
        var tmpNext = $(focused).next().get(0);
        if (tmpNext) next = tmpNext;
      }

      if (next) next.classList.add('active');
    };

    _proto._focusPrev = function _focusPrev() {
      var next = $(this._dropdown).children(':last-child').get(0);
      var focused = $(this._dropdown).children('.active').get(0);

      if (focused) {
        focused.classList.remove('active');
        var tmpNext = $(focused).prev().get(0);
        if (tmpNext) next = tmpNext;
      }

      if (next) next.classList.add('active');
    };

    _proto._getConfig = function _getConfig(config) {
      config = _objectSpread({}, Default, config);
      Util.typeCheckConfig(NAME, config, DefaultType);
      return config;
    };

    _proto._handleRelations = function _handleRelations() {
      var _this5 = this;

      this._relations = [];

      for (var name in this._config.filterRelation) {
        var selector = this._config.filterRelation[name];
        this._relations[name] = $(selector).get(0);
        $(this._relations[name]).change(function (e) {
          _this5._element.value = '';
          $(_this5._element).change(); // we need to trigger this manually

          _this5._items = [];
        });
      }
    };

    _proto._hideDropdown = function _hideDropdown() {
      this._isShown = false;

      this._dropdown.classList.remove('show');
    };

    _proto._hideSpinner = function _hideSpinner() {
      this._spinner.style.display = 'none';
    };

    _proto._makeDropdown = function _makeDropdown() {
      this._element.parentNode.style.position = 'relative';
      var tmpl = '<div class="dropdown-menu" style="width:100%"></div>';
      this._dropdown = $(tmpl).appendTo(this._element.parentNode).get(0);
    };

    _proto._makeSpinner = function _makeSpinner() {
      var tmpl = '<div class="spinner-border spinner-border-sm text-secondary" role="status"></div>';
      this._spinner = $(tmpl).appendTo(this._element.parentNode).get(0);
      $(this._spinner).css({
        position: 'absolute',
        right: $(this._element).css('padding-right'),
        top: this._element.offsetTop + (this._element.offsetHeight - this._spinner.offsetHeight) / 2 + 'px'
      });

      this._hideSpinner();
    };

    _proto._renderItem = function _renderItem(items) {
      var _this6 = this;

      items.forEach(function (i) {
        if (_this6._dropdown.children.length >= _this6._config.maxResult) return;
        var item = $('<a class="dropdown-item" href="#"></a>');
        var itemEl = item.get(0);
        item.text(i).appendTo(_this6._dropdown);
        if (_this6._config.onItemRendered) _this6._config.onItemRendered(_this6._element, itemEl);
        var renderEvent = $.Event(Event.ITEM_RENDER, {
          item: itemEl
        });
        $(_this6._element).trigger(renderEvent);
        item.on(Event.CLICK_DATA_API, function (e) {
          _this6._selectItem(e.target);

          _this6._preventClose = true;

          _this6.hide();

          e.preventDefault();
          _this6._preventClose = false;
        });
      });
    };

    _proto._selectItem = function _selectItem(item) {
      this._element.value = item.innerText;
      if (this._config.onPick) this._config.onPick(this._element, item);
      var pickEvent = $.Event(Event.PICK, {
        item: item
      });
      $(this._element).trigger(pickEvent);
    };

    _proto._showDropdown = function _showDropdown() {
      this._isShown = true;

      this._dropdown.classList.add('show');
    };

    _proto._showSpinner = function _showSpinner() {
      this._spinner.style.display = 'inline-block';
    };

    _proto._truncateDropdown = function _truncateDropdown() {
      this._dropdown.innerHTML = '';
    } // Static
    ;

    Autocomplete._jQueryInterface = function _jQueryInterface(config, relatedTarget) {
      return this.each(function () {
        var data = $(this).data(DATA_KEY);

        var _config = _objectSpread({}, Default, $(this).data(), typeof config === 'object' && config ? config : {});

        if (!data) {
          data = new Autocomplete(this, _config);
          $(this).data(DATA_KEY, data);
        }

        if (typeof config === 'string') {
          if (typeof data[config] === 'undefined') throw new TypeError("No method named \"" + config + "\"");
          data[config](relatedTarget);
        }
      });
    };

    _createClass(Autocomplete, null, [{
      key: "VERSION",
      get: function get() {
        return VERSION;
      }
    }, {
      key: "Default",
      get: function get() {
        return Default;
      }
    }]);

    return Autocomplete;
  }();
  /**
   * ------------------------------------------------------------------------
   * jQuery
   * ------------------------------------------------------------------------
   */


  $.fn[NAME] = Autocomplete._jQueryInterface;
  $.fn[NAME].Constructor = Autocomplete;

  $.fn[NAME].noConflict = function () {
    $.fn[NAME] = JQUERY_NO_CONFLICT;
    return Autocomplete._jQueryInterface;
  };

  exports.Autocomplete = Autocomplete;

  Object.defineProperty(exports, '__esModule', { value: true });

})));
//# sourceMappingURL=bootstrap-autocomplete.js.map
