using System.Text.Json;

namespace KuaiJi.Modules.Tax.Infrastructure
{
    /// <summary>
    /// 服务端登录税局时注入页面的脚本。选择器沿用本机助手验证过的一套：
    /// 税局页面各省 UI 不同、class 名不可依赖，只能按可见元素的文案与输入框语义定位。
    /// 所有结构化结果统一 JSON.stringify 回传，由 C# 侧解析。
    /// </summary>
    internal static class TaxPortalPageScripts
    {
        /// <summary>去掉最明显的无头浏览器特征；税局的风控脚本会看 navigator.webdriver。</summary>
        public const string StealthScript = """
Object.defineProperty(navigator, 'webdriver', { get: () => undefined });
Object.defineProperty(navigator, 'languages', { get: () => ['zh-CN', 'zh'] });
""";

        public static readonly string PageReady = Js("""
if (document.readyState === 'loading') return false;
if (document.querySelector('input[type=password]')) return true;
const body = document.body ? document.body.innerText : '';
return /(电子税务局|统一身份认证|用户登录|企业业务|自然人业务|代理机构)/.test(body);
""");

        public static readonly string HasPassword = Js("""
return !!document.querySelector('input[type=password]');
""");

        /// <summary>
        /// 页面是否已经退回登录态：只看可见的密码框或明确的超时提示，
        /// 已登录页面里藏着的登录表单（隐藏的切换账号弹层）不算。
        /// </summary>
        public static readonly string SessionExpired = Js("""
const visiblePwd = Array.from(document.querySelectorAll('input[type=password]')).some(visible);
const body = document.body ? document.body.innerText : '';
const expired = /(登录超时|登录状态已失效|会话已过期|登录已过期|登录会话已失效)/.test(body);
return visiblePwd || expired;
""");

        public static readonly string ClickLoginEntry = Js("""
const exact = ['登录', '登 录', '立即登录', '用户登录', '企业登录'];
const nodes = Array.from(document.querySelectorAll('a,button,span,div')).filter(visible);
const hits = nodes.filter(el => {
  const t = textOf(el);
  return exact.indexOf(t) >= 0 && !/(扫码|短信|忘记|注册|新办)/.test(t);
});
const anchor = hits.find(el => el.tagName === 'A' && /(login|tpass)/i.test(el.href || ''));
const hit = anchor || hits[0];
if (hit) { hit.click(); return true; }
const link = Array.from(document.querySelectorAll('a[href]')).filter(visible).find(a => /oauth2\/login/i.test(a.href || ''));
if (link) { link.click(); return true; }
return false;
""");

        public static readonly string ReadLoginLink = Js("""
const link = Array.from(document.querySelectorAll('a[href]')).map(a => a.href).find(h => /oauth2\/login\?/i.test(h));
return { url: link || '' };
""");

        public static readonly string DiagnosePage = Js("""
const list = inputs().slice(0, 12).map(el => ({
  type: el.type || '',
  name: el.name || '',
  id: el.id || '',
  placeholder: (el.placeholder || '').slice(0, 40),
  label: describe(el).slice(0, 60)
}));
return {
  url: location.href,
  title: document.title,
  count: inputs().length,
  inputs: list,
  text: (document.body ? document.body.innerText : '').replace(/\s+/g, ' ').slice(0, 300)
};
""");

        public static string SelectTab(string? loginType)
        {
            string wanted = JsonSerializer.Serialize(loginType ?? string.Empty);
            return Js("const wanted = " + wanted + ";\n" + """
const isAgency = wanted.indexOf('代理') >= 0;
const re = isAgency ? /(代理.*登录|代理机构|代理业务)/ : /企业业务/;
const nodes = Array.from(document.querySelectorAll('[role=tab],div,span,a,li,button')).filter(visible);
const hit = nodes.find(el => re.test(textOf(el)) && textOf(el).length <= 12);
if (hit) { hit.click(); return true; }
return false;
""");
        }

        public static string FillLogin(string taxNo, string account, string password)
        {
            string taxNoJson = JsonSerializer.Serialize(taxNo);
            string accountJson = JsonSerializer.Serialize(account);
            string passwordJson = JsonSerializer.Serialize(password);
            return Js("const taxNo = " + taxNoJson + ";\nconst account = " + accountJson + ";\nconst password = " + passwordJson + ";\n" + """
const excluded = ['hidden', 'checkbox', 'radio', 'button', 'submit', 'file', 'image', 'range', 'color', 'date', 'time', 'search'];
const fields = inputs().filter(el => excluded.indexOf(el.type) < 0);
const passwordEl = fields.find(el => el.type === 'password');
const textEls = fields.filter(el => el.type !== 'password' && !/(图形验证码|图片验证码)/.test(describe(el)));
const taxNoRe = /(统一社会信用代码|纳税人识别号|纳税人识别|社会信用代码|税号|taxpayer|taxno|nsrsbh|shxydm|creditcode)/i;
const accountRe = /(用户名|手机号码|手机号|身份证|账号|账户|用户|account|username|loginname|userid|mobile|phone)/i;
let taxNoEl = textEls.find(el => taxNoRe.test(describe(el)));
let accountEl = textEls.find(el => el !== taxNoEl && accountRe.test(describe(el)));
if (!taxNoEl && textEls.length > 0) taxNoEl = textEls[0];
if (!accountEl) accountEl = textEls.find(el => el !== taxNoEl) || null;
if (taxNoEl) setValue(taxNoEl, taxNo);
if (accountEl) setValue(accountEl, account);
if (passwordEl) setValue(passwordEl, password);
return { ok: !!passwordEl && !!taxNoEl, taxNo: !!taxNoEl, account: !!accountEl, password: !!passwordEl };
""");
        }

        public static readonly string DetectSlider = Js("""
const hint = /(拖动滑块|按住滑块|向右拖动|滑动验证|请完成.*验证|拖动下方滑块|按住左边滑块|拖动到最右)/;
const hit = Array.from(document.querySelectorAll('div,span,p,button')).filter(visible).find(el => hint.test(textOf(el)) && textOf(el).length < 40);
return { slider: !!hit };
""");

        public static readonly string DetectImageCaptcha = Js("""
const el = inputs().find(i => i.type !== 'password' && /(图形验证码|图片验证码)/.test(describe(i)));
return { image: !!el, empty: !!el && !el.value };
""");

        public static readonly string ClickLogin = Js("""
const exact = ['登录', '登 录', '立即登录'];
const nodes = Array.from(document.querySelectorAll('button, a, div, span')).filter(visible);
const hits = nodes.filter(el => {
  const t = textOf(el);
  return exact.indexOf(t) >= 0 && !/扫码|短信|忘记|注册/.test(t);
});
const hit = hits.find(el => el.tagName === 'BUTTON') || hits[hits.length - 1];
if (!hit) return false;
hit.click();
return true;
""");

        public static readonly string ClickSendSms = Js("""
const re = /(获取验证码|获取短信验证码|发送验证码|免费获取|点击获取|获取动态码)/;
const nodes = Array.from(document.querySelectorAll('button, a, span, div')).filter(visible);
const hit = nodes.reverse().find(el => {
  const t = textOf(el);
  return re.test(t) && t.length <= 12 && !/秒|重新获取/.test(t);
});
if (!hit) return false;
hit.click();
return true;
""");

        public static string FillSms(string code)
        {
            string codeJson = JsonSerializer.Serialize(code);
            return Js("const code = " + codeJson + ";\n" + """
const el = inputs().find(i => i.type !== 'password' && /(短信验证码|手机验证码|动态码|验证码)/.test(describe(i)) && !/(图形|图片)/.test(describe(i)));
if (!el) return { filled: false };
setValue(el, code);
return { filled: true };
""");
        }

        public static readonly string ClickConfirm = Js("""
const exact = ['确定', '确认', '提交', '验证', '登录', '下一步', '完成'];
const nodes = Array.from(document.querySelectorAll('button, a, span, div')).filter(visible);
const hit = nodes.reverse().find(el => {
  const t = textOf(el);
  return exact.indexOf(t) >= 0 && t.length <= 4;
});
if (!hit) return false;
hit.click();
return true;
""");

        public static readonly string DismissDialog = Js("""
const exact = ['我知道了', '知道了', '同意', '关闭', '继续登录'];
const nodes = Array.from(document.querySelectorAll('button, a, span, div')).filter(visible);
const hit = nodes.reverse().find(el => exact.indexOf(textOf(el)) >= 0 && textOf(el).length <= 6);
if (!hit) return false;
hit.click();
return true;
""");

        public static readonly string Stage = Js("""
const body = document.body ? document.body.innerText : '';
const hasPassword = inputs().some(i => i.type === 'password');
const successByText = !hasPassword && /(退出登录|退出|我的信息|我要办税|办税中心|用户中心)/.test(body);
const successByUrl = !hasPassword && /(userCenter|dashboard|\/home)/i.test(location.pathname);
const smsInput = inputs().find(i => i.type !== 'password' && /(短信验证码|手机验证码|动态码|验证码)/.test(describe(i)) && !/(图形|图片)/.test(describe(i)));
const captchaBox = document.querySelector('#captcha-box, .slide-verify-new');
const captchaImg = document.querySelector('#tpass-captcha-slider-bg-img');
const captcha = (!!captchaBox && visible(captchaBox)) || (!!captchaImg && visible(captchaImg));
return { success: successByText || successByUrl, sms: !!smsInput, captcha };
""");

        /// <summary>
        /// 文字点选验证码的底图与提示字。税局把两张图都以 data URL 内嵌，取原图比截屏清晰得多
        /// （页面上显示尺寸只有原图的四五成）；宽高用元素实际显示尺寸，点选坐标按它归一化。
        /// </summary>
        public static readonly string CaptchaGeometry = Js("""
const img = document.querySelector('#tpass-captcha-slider-bg-img');
const tip = document.querySelector('#tpass-captcha-tip-img');
const rect = img ? img.getBoundingClientRect() : null;
const srcOf = (el) => el ? (el.currentSrc || el.src || '') : '';
return {
  present: !!img && visible(img),
  prompt: !!tip && visible(tip),
  width: rect ? rect.width : 0,
  height: rect ? rect.height : 0,
  image: srcOf(img),
  promptImage: srcOf(tip)
};
""");

        /// <summary>
        /// 页内自动点选：拿提示字逐个在底图上做形状匹配（旋转 ±60°、缩放 0.85~1.3、长宽比 ±10% 都搜一遍），
        /// 再做一次「每字一个位置、互相至少 42px」的联合分配，返回底图原始像素坐标。
        /// 底图特征只看色度（max-min）：白色花瓣、亮天空这类高亮背景与字形无关，亮度通道反而会把它们算进来；
        /// 两个色度场——残差（细笔画偏离局部背景）与顶帽（比整块色块更细的结构）——取较大分。
        /// 入参是两张 data URL（提示字、底图），返回 { glyphs, bg:[宽,高], clicks:[{x,y,score}], ms }，
        /// 坐标是底图原始像素。
        /// </summary>
        public static readonly string SolveCaptcha = """
async ([tipUrl, bgUrl]) => {
  const loadImg = (url) => new Promise((resolve, reject) => {
    const img = new Image();
    img.onload = () => resolve(img);
    img.onerror = reject;
    img.src = url;
  });

  const toData = (img) => {
    const c = document.createElement('canvas');
    c.width = img.naturalWidth;
    c.height = img.naturalHeight;
    const ctx = c.getContext('2d', { willReadFrequently: true });
    ctx.drawImage(img, 0, 0);
    const d = ctx.getImageData(0, 0, c.width, c.height);
    return { w: d.width, h: d.height, data: d.data };
  };

  const gaussBlur = (src, w, h, sigma) => {
    const r = Math.max(1, Math.round(sigma * 2.5));
    const k = new Float32Array(2 * r + 1);
    let sum = 0;
    for (let i = -r; i <= r; i++) { const v = Math.exp(-(i * i) / (2 * sigma * sigma)); k[i + r] = v; sum += v; }
    for (let i = 0; i < k.length; i++) k[i] /= sum;
    const tmp = new Float32Array(w * h), out = new Float32Array(w * h);
    for (let y = 0; y < h; y++) {
      for (let x = 0; x < w; x++) {
        let acc = 0;
        for (let t = -r; t <= r; t++) acc += src[y * w + Math.min(w - 1, Math.max(0, x + t))] * k[t + r];
        tmp[y * w + x] = acc;
      }
    }
    for (let y = 0; y < h; y++) {
      for (let x = 0; x < w; x++) {
        let acc = 0;
        for (let t = -r; t <= r; t++) acc += tmp[Math.min(h - 1, Math.max(0, y + t)) * w + x] * k[t + r];
        out[y * w + x] = acc;
      }
    }
    return out;
  };

  const downsample = (src, w, h, f) => {
    const nw = Math.floor(w / f), nh = Math.floor(h / f);
    const out = new Float32Array(nw * nh);
    for (let y = 0; y < nh; y++) {
      for (let x = 0; x < nw; x++) {
        let acc = 0;
        for (let j = 0; j < f; j++) for (let i = 0; i < f; i++) acc += src[(y * f + j) * w + x * f + i];
        out[y * nw + x] = acc / (f * f);
      }
    }
    return { w: nw, h: nh, a: out };
  };

  const integral = (a, w, h) => {
    const S = new Float64Array((w + 1) * (h + 1));
    for (let y = 0; y < h; y++) {
      let rs = 0;
      const o = (y + 1) * (w + 1), po = y * (w + 1);
      for (let x = 0; x < w; x++) { rs += a[y * w + x]; S[o + x + 1] = S[po + x + 1] + rs; }
    }
    return S;
  };

  const boxSum = (S, w, x, y, bw, bh) => {
    const sw = w + 1;
    return S[(y + bh) * sw + x + bw] - S[(y + bh) * sw + x] - S[y * sw + x + bw] + S[y * sw + x];
  };

  // 提示字：灰色字形 + 噪线，按列投影切成独立字块
  const promptGlyphs = (px) => {
    const w = px.w, h = px.h, d = px.data;
    const mask = new Uint8Array(w * h);
    for (let i = 0, p = 0; i < w * h; i++, p += 4) {
      const mx = Math.max(d[p], d[p + 1], d[p + 2]), mn = Math.min(d[p], d[p + 1], d[p + 2]);
      if (d[p + 3] > 10 && mx < 215 && mx - mn < 28) mask[i] = 1;
    }
    const cols = new Int32Array(w);
    for (let x = 0; x < w; x++) { let c = 0; for (let y = 0; y < h; y++) c += mask[y * w + x]; cols[x] = c; }
    const runs = [];
    let inside = false, start = 0;
    for (let x = 0; x < w; x++) {
      if (cols[x] > 0 && !inside) { start = x; inside = true; }
      if (cols[x] === 0 && inside) { runs.push([start, x - 1]); inside = false; }
    }
    if (inside) runs.push([start, w - 1]);
    const merged = [];
    for (const [s, e] of runs) {
      if (merged.length && s - merged[merged.length - 1][1] <= 4) merged[merged.length - 1][1] = e;
      else merged.push([s, e]);
    }
    const out = [];
    for (const [x0, x1] of merged) {
      let y0 = -1, y1 = -1;
      for (let y = 0; y < h; y++) {
        let hit = false;
        for (let x = x0; x <= x1; x++) if (mask[y * w + x]) { hit = true; break; }
        if (hit) { if (y0 < 0) y0 = y; y1 = y; }
      }
      if (y0 < 0) continue;
      const gw = x1 - x0 + 1, gh = y1 - y0 + 1;
      const m = new Float32Array(gw * gh);
      for (let y = 0; y < gh; y++) for (let x = 0; x < gw; x++) m[y * gw + x] = mask[(y0 + y) * w + x0 + x];
      out.push({ w: gw, h: gh, mask: m });
    }
    return out;
  };

  const minmax1d = (src, w, h, k, isMin) => {
    const r = (k - 1) >> 1;
    const tmp = new Float32Array(w * h), out = new Float32Array(w * h);
    const init = isMin ? 1e9 : -1e9;
    for (let y = 0; y < h; y++) {
      for (let x = 0; x < w; x++) {
        let v = init;
        for (let t = -r; t <= r; t++) {
          let xx = x + t; if (xx < 0) xx = 0; else if (xx >= w) xx = w - 1;
          const s = src[y * w + xx];
          v = isMin ? (s < v ? s : v) : (s > v ? s : v);
        }
        tmp[y * w + x] = v;
      }
    }
    for (let y = 0; y < h; y++) {
      for (let x = 0; x < w; x++) {
        let v = init;
        for (let t = -r; t <= r; t++) {
          let yy = y + t; if (yy < 0) yy = 0; else if (yy >= h) yy = h - 1;
          const s = tmp[yy * w + x];
          v = isMin ? (s < v ? s : v) : (s > v ? s : v);
        }
        out[y * w + x] = v;
      }
    }
    return out;
  };

  const bgFields = (px, sigma) => {
    const w = px.w, h = px.h, d = px.data;
    const ch = new Float32Array(w * h);
    for (let i = 0, p = 0; i < w * h; i++, p += 4) {
      ch[i] = Math.max(d[p], d[p + 1], d[p + 2]) - Math.min(d[p], d[p + 1], d[p + 2]);
    }
    const bgCh = gaussBlur(ch, w, h, 8);
    const resid = new Float32Array(w * h);
    for (let i = 0; i < resid.length; i++) resid[i] = Math.abs(ch[i] - bgCh[i]);
    const opened = minmax1d(minmax1d(ch, w, h, 9, true), w, h, 9, false);
    const top = new Float32Array(w * h);
    for (let i = 0; i < top.length; i++) { const v = ch[i] - opened[i]; top[i] = v < 0 ? 0 : v; }
    return [gaussBlur(resid, w, h, sigma), gaussBlur(top, w, h, sigma)];
  };

  const bilinear = (a, w, h, x, y) => {
    const x0 = Math.floor(x), y0 = Math.floor(y);
    const x1 = Math.min(w - 1, x0 + 1), y1 = Math.min(h - 1, y0 + 1);
    const fx = x - x0, fy = y - y0;
    const v00 = a[y0 * w + x0] || 0, v10 = a[y0 * w + x1] || 0, v01 = a[y1 * w + x0] || 0, v11 = a[y1 * w + x1] || 0;
    return (v00 * (1 - fx) + v10 * fx) * (1 - fy) + (v01 * (1 - fx) + v11 * fx) * fy;
  };

  // 旋转 + 各向异性缩放进正方形画布
  const rotPad = (tpl, deg, scale, sy) => {
    const cx = tpl.w / 2, cy = tpl.h / 2, s = scale || 1, s2 = sy || s;
    const diag = Math.ceil(Math.sqrt(tpl.w * tpl.w + tpl.h * tpl.h) * Math.max(s, s2)) + 2;
    const out = new Float32Array(diag * diag);
    const rad = (deg * Math.PI) / 180, cos = Math.cos(rad), sin = Math.sin(rad), mid = diag / 2;
    for (let y = 0; y < diag; y++) {
      for (let x = 0; x < diag; x++) {
        const dx = (x + 0.5 - mid) / s, dy = (y + 0.5 - mid) / s2;
        const sx = cos * dx + sin * dy + cx - 0.5;
        const sy2 = -sin * dx + cos * dy + cy - 0.5;
        if (sx < -0.5 || sy2 < -0.5 || sx > tpl.w - 0.5 || sy2 > tpl.h - 0.5) continue;
        out[y * diag + x] = bilinear(tpl.mask, tpl.w, tpl.h, sx, sy2);
      }
    }
    return { w: diag, h: diag, mask: out };
  };

  const prepTpl = (tpl, sigma) => {
    const m = sigma ? gaussBlur(tpl.mask, tpl.w, tpl.h, sigma) : tpl.mask;
    const n = tpl.w * tpl.h;
    let mt = 0;
    for (let i = 0; i < n; i++) mt += m[i];
    mt /= n;
    const T = new Float32Array(n);
    let tss = 0;
    for (let i = 0; i < n; i++) { T[i] = m[i] - mt; tss += T[i] * T[i]; }
    return { w: tpl.w, h: tpl.h, T, norm: Math.sqrt(tss) };
  };

  const scoreAt = (fd, tpl, x, y) => {
    const w = fd.w, h = fd.h, field = fd.a, tw = tpl.w, th = tpl.h, n = tw * th;
    if (x < 0 || y < 0 || x + tw > w || y + th > h) return -2;
    let num = 0;
    for (let j = 0; j < th; j++) {
      const ro = j * tw, fo = (y + j) * w + x;
      for (let i = 0; i < tw; i++) num += tpl.T[ro + i] * field[fo + i];
    }
    const s1 = boxSum(fd.S1, w, x, y, tw, th), s2 = boxSum(fd.S2, w, x, y, tw, th);
    const varv = Math.max(s2 - (s1 * s1) / n, 1e-3);
    return num / (Math.sqrt(varv) * tpl.norm + 1e-6);
  };

  const scoreAtBest = (fds, tpl, x, y) => {
    let best = -2;
    for (let i = 0; i < fds.length; i++) {
      const s = scoreAt(fds[i], tpl, x, y);
      if (s > best) best = s;
    }
    return best;
  };

  const scanPeaks = (fd, tpl, topk, minDist) => {
    const w = fd.w, h = fd.h, field = fd.a, tw = tpl.w, th = tpl.h;
    if (tw >= w || th >= h || tpl.norm < 1e-6) return [];
    const ow = w - tw + 1, oh = h - th + 1, n = tw * th;
    const score = new Float32Array(ow * oh);
    for (let y = 0; y < oh; y++) {
      for (let x = 0; x < ow; x++) {
        let num = 0;
        for (let j = 0; j < th; j++) {
          const ro = j * tw, fo = (y + j) * w + x;
          for (let i = 0; i < tw; i++) num += tpl.T[ro + i] * field[fo + i];
        }
        const s1 = boxSum(fd.S1, w, x, y, tw, th), s2 = boxSum(fd.S2, w, x, y, tw, th);
        const varv = Math.max(s2 - (s1 * s1) / n, 1e-3);
        score[y * ow + x] = num / (Math.sqrt(varv) * tpl.norm + 1e-6);
      }
    }
    const peaks = [];
    for (let r = 0; r < topk; r++) {
      let best = -2, bi = -1;
      for (let k = 0; k < score.length; k++) if (score[k] > best) { best = score[k]; bi = k; }
      if (bi < 0 || best <= -1) break;
      const py = Math.floor(bi / ow), px = bi % ow;
      peaks.push({ score: best, x: px + tw / 2, y: py + th / 2 });
      for (let yy = Math.max(0, py - minDist); yy <= Math.min(oh - 1, py + minDist); yy++)
        for (let xx = Math.max(0, px - minDist); xx <= Math.min(ow - 1, px + minDist); xx++) score[yy * ow + xx] = -2;
    }
    return peaks;
  };

  const makeSet = (a, w, h, factor) => {
    const S1 = integral(a, w, h);
    const sq = new Float32Array(a.length);
    for (let i = 0; i < sq.length; i++) sq[i] = a[i] * a[i];
    const S2 = integral(sq, w, h);
    const ds = downsample(a, w, h, factor);
    const sS1 = integral(ds.a, ds.w, ds.h);
    const ssq = new Float32Array(ds.a.length);
    for (let i = 0; i < ssq.length; i++) ssq[i] = ds.a[i] * ds.a[i];
    const sS2 = integral(ssq, ds.w, ds.h);
    return { w, h, a, S1, S2, sw: ds.w, sh: ds.h, sa: ds.a, sS1, sS2 };
  };

  const t0 = performance.now();
  const factor = 4;
  const coarseAngles = [-60, -50, -40, -30, -20, -10, 0, 10, 20, 30, 40, 50, 60];
  const coarseScales = [0.85, 1.0, 1.15, 1.3];
  const candPer = 5;
  const minSep = 42;

  const tip = toData(await loadImg(tipUrl));
  const bg = toData(await loadImg(bgUrl));
  const glyphs = promptGlyphs(tip);
  const fds = bgFields(bg, 2).map((a) => makeSet(a, bg.w, bg.h, factor));
  const items = [];
  if (!glyphs.length) return JSON.stringify({ glyphs: 0, bg: [bg.w, bg.h], clicks: [], ms: 0 });

  for (const g of glyphs) {
    const smallMask = downsample(g.mask, g.w, g.h, factor);
    const base = { w: smallMask.w, h: smallMask.h, mask: smallMask.a };

    // 粗匹配：1/4 分辨率下两个场各自扫描，取高分峰
    const all = [];
    for (const deg of coarseAngles) {
      for (const scale of coarseScales) {
        const tpl = prepTpl(rotPad(base, deg, scale));
        for (const fd of fds) {
          const pk = scanPeaks({ w: fd.sw, h: fd.sh, a: fd.sa, S1: fd.sS1, S2: fd.sS2 }, tpl, 1, 8);
          for (const p of pk) all.push({ score: p.score, x: p.x * factor, y: p.y * factor, deg, scale });
        }
      }
    }
    all.sort((a, b) => b.score - a.score);
    const coarse = [];
    for (const c of all) {
      if (coarse.length >= candPer) break;
      if (!coarse.some((k) => (k.x - c.x) ** 2 + (k.y - c.y) ** 2 < 35 * 35)) coarse.push(c);
    }

    // 细匹配：全分辨率局部搜索（位置 ±7px、角度 ±8°、缩放/长宽比各 ±10%）
    const cands = [];
    for (const c of coarse) {
      let bestC = null;
      for (let ds = 0; ds < 3; ds++) {
        const sc = c.scale * (0.9 + 0.1 * ds);
        for (let ar = 0; ar < 3; ar++) {
          const aspect = 0.9 + 0.1 * ar;
          for (let da = -8; da <= 8; da += 4) {
            const t2 = prepTpl(rotPad(g, c.deg + da, sc, sc * aspect), 2);
            for (let dy = -7; dy <= 7; dy += 3.5) {
              for (let dx = -7; dx <= 7; dx += 3.5) {
                const cx = Math.round(c.x + dx - t2.w / 2), cy = Math.round(c.y + dy - t2.h / 2);
                const score = scoreAtBest(fds, t2, cx, cy);
                if (!bestC || score > bestC.score) {
                  bestC = { score, x: cx + t2.w / 2, y: cy + t2.h / 2, deg: c.deg + da, scale: sc };
                }
              }
            }
          }
        }
      }
      if (bestC) cands.push(bestC);
    }
    cands.sort((a, b) => b.score - a.score);
    const kept = [];
    for (const c of cands) {
      if (!kept.some((k) => (k.x - c.x) ** 2 + (k.y - c.y) ** 2 < 35 * 35)) kept.push(c);
    }
    items.push(kept);
  }

  // 联合分配：每个提示字占一个位置，位置之间至少相隔 minSep
  let bestCombo = null, bestScore = -1e9;
  const walk = (pos, acc, chosen) => {
    if (pos === glyphs.length) {
      if (acc > bestScore) { bestScore = acc; bestCombo = chosen.slice(); }
      return;
    }
    for (const c of items[pos]) {
      if (chosen.some((k) => (k.x - c.x) ** 2 + (k.y - c.y) ** 2 < minSep * minSep)) continue;
      chosen.push(c);
      walk(pos + 1, acc + c.score, chosen);
      chosen.pop();
    }
    walk(pos + 1, acc, chosen);
  };
  walk(0, 0, []);

  const clicks = (bestCombo || []).map((c) => ({
    x: Math.round(c.x),
    y: Math.round(c.y),
    score: Math.round(c.score * 1000) / 1000,
  }));
  return JSON.stringify({ glyphs: glyphs.length, bg: [bg.w, bg.h], clicks, ms: Math.round(performance.now() - t0) });
}
""";

        /// <summary>点「确定」提交本轮的验证码点选。</summary>
        public static readonly string ClickCaptchaConfirm = Js("""
const scope = document.querySelector('#captcha-box, .slide-verify-new') || document;
const direct = scope.querySelector('.click-confirm-btn');
if (direct && visible(direct)) { direct.click(); return true; }
const hit = Array.from(scope.querySelectorAll('button, div, span, a')).filter(visible).find(el => textOf(el) === '确定');
if (!hit) return false;
hit.click();
return true;
""");

        /// <summary>
        /// 按文案点一个菜单项。税局的菜单层级是折叠的，这里只负责点当前层可见的那一项，
        /// 逐级往下走由 C# 侧控制；先发 hover 再点，兼容只用鼠标悬停展开的菜单。
        /// </summary>
        public static string ClickMenuText(string text)
        {
            string textJson = JsonSerializer.Serialize(text);
            return Js("const wanted = " + textJson + ";\n" + """
const nodes = Array.from(document.querySelectorAll('a,button,span,div,li')).filter(visible);
const hits = nodes.filter(el => {
  const t = textOf(el);
  return t === wanted || (t.indexOf(wanted) >= 0 && t.length <= wanted.length + 8);
});
const hit = hits.find(el => el.tagName === 'A' || el.tagName === 'BUTTON')
  || hits.find(el => el.tagName === 'LI')
  || hits.slice().sort((a, b) => textOf(a).length - textOf(b).length)[0];
if (!hit) return { clicked: false };
const fire = (type) => hit.dispatchEvent(new MouseEvent(type, { bubbles: true, cancelable: true, view: window }));
fire('mouseenter');
fire('mouseover');
hit.click();
return { clicked: true, text: textOf(hit) };
""");
        }

        /// <summary>菜单定位失败时，把当前页面所有短文案回传，便于按真实结构补路径。</summary>
        public static readonly string MenuOutline = Js("""
const nodes = Array.from(document.querySelectorAll('a,button,span,div,li')).filter(visible);
const seen = [];
for (const el of nodes) {
  const t = textOf(el);
  if (!t || t.length > 12) continue;
  if (seen.indexOf(t) >= 0) continue;
  seen.push(t);
  if (seen.length >= 80) break;
}
return { url: location.href, title: document.title, texts: seen };
""");

        public static readonly string ReadErrorText = Js("""
const re = /(验证码.*(错误|无效|过期)|用户名.*(错误|不存在)|密码.*(错误|不正确)|登录失败|认证失败|账号.*锁定|请重新输入)/;
const nodes = Array.from(document.querySelectorAll('div,span,p,label,li')).filter(visible);
const hit = nodes.map(textOf).find(t => re.test(t) && t.length <= 80);
return { text: hit || '' };
""");

        /// <summary>
        /// 包一层返回值归一化：Playwright 的 EvaluateAsync&lt;string&gt; 只接受字符串，
        /// 脚本里直接 return 对象会在反序列化时失败，所以统一 stringify 后再回传。
        /// </summary>
        public static string AsJson(string script)
        {
            return "() => { const r = (" + script + ")(); return typeof r === 'string' ? r : JSON.stringify(r); }";
        }

        private static string Js(string body)
        {
            return "() => {\n" + Helpers + "\n" + body + "\n}";
        }

        private const string Helpers = """
const visible = (el) => {
  if (!el) return false;
  const r = el.getBoundingClientRect();
  if (r.width <= 0 || r.height <= 0) return false;
  const s = getComputedStyle(el);
  return s.display !== 'none' && s.visibility !== 'hidden' && s.opacity !== '0';
};
const textOf = (el) => ((el.innerText || el.textContent || '').replace(/\s+/g, ''));
const describe = (el) => {
  let t = '';
  if (el.id) {
    const label = document.querySelector('label[for="' + CSS.escape(el.id) + '"]');
    if (label) t += ' ' + textOf(label);
  }
  const wrap = el.closest('.ant-form-item, .el-form-item, .form-item, .form-group, .input-group, .ivu-form-item, tr');
  if (wrap && wrap !== el) t += ' ' + textOf(wrap).slice(0, 80);
  t += ' ' + (el.getAttribute('placeholder') || '') + ' ' + (el.getAttribute('aria-label') || '') + ' ' + (el.name || '') + ' ' + (el.id || '');
  return t;
};
const inputs = () => Array.from(document.querySelectorAll('input')).filter(visible).filter(el => !el.disabled && !el.readOnly);
const setValue = (el, value) => {
  const proto = el instanceof HTMLTextAreaElement ? HTMLTextAreaElement.prototype : HTMLInputElement.prototype;
  const setter = Object.getOwnPropertyDescriptor(proto, 'value').set;
  setter.call(el, value);
  el.dispatchEvent(new Event('input', { bubbles: true }));
  el.dispatchEvent(new Event('change', { bubbles: true }));
  el.dispatchEvent(new Event('blur', { bubbles: true }));
};
""";
    }
}
