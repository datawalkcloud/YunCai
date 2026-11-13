using System.Text.Json;

namespace YunCai.Modules.Tax.Infrastructure
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

        /// <summary>
        /// 只点文案完全一致的元素。查询页上同时挂着「全量发票查询」菜单和「查询」按钮，
        /// 宽匹配会误点菜单、请求根本没发出去。
        /// </summary>
        public static string ClickExactText(string text)
        {
            string textJson = JsonSerializer.Serialize(text);
            return Js("const wanted = " + textJson + ";\n" + """
const nodes = Array.from(document.querySelectorAll('button,a,span,div,li')).filter(visible);
const hits = nodes.filter(el => textOf(el) === wanted);
const hit = hits.find(el => el.tagName === 'BUTTON')
  || hits.find(el => el.tagName === 'A')
  || hits.find(el => el.tagName === 'SPAN')
  || hits[0];
if (!hit) return { clicked: false };
const fire = (type) => hit.dispatchEvent(new MouseEvent(type, { bubbles: true, cancelable: true, view: window }));
fire('mouseenter');
fire('mouseover');
hit.click();
return { clicked: true, tag: hit.tagName, text: textOf(hit) };
""");
        }

        /// <summary>
        /// 填开票日期起止。税局日期控件的占位符各省写法不一（「开票日期（起）」「请选择开票日期起」），
        /// 还有不少是 readonly + 日历控件，直接 Type 不进值，这里按标签找输入框后走原生 setter。
        /// </summary>
        public static string FillInvoiceDates(string start, string end)
        {
            string startJson = JsonSerializer.Serialize(start);
            string endJson = JsonSerializer.Serialize(end);
            return Js("const startValue = " + startJson + ";\nconst endValue = " + endJson + ";\n" + """
const all = Array.from(document.querySelectorAll('input')).filter(visible).filter(el => el.type !== 'hidden');
const labelOf = (el) => {
  let t = (el.getAttribute('placeholder') || '') + ' ' + (el.getAttribute('aria-label') || '');
  t += ' ' + (el.name || '') + ' ' + (el.id || '');
  const wrap = el.closest('.ant-form-item, .el-form-item, .form-item, .form-group, .input-group, .ivu-form-item, tr, div');
  if (wrap) t += ' ' + textOf(wrap).slice(0, 40);
  return t;
};
const pick = (re, exclude) => all.find(el => {
  const d = labelOf(el);
  return re.test(d) && !(exclude && exclude.test(d));
});
let startEl = pick(/(开票日期|开票时间).*(起|开始|起始)|起始日期|开始日期/, /(止|结束|截止)/);
let endEl = pick(/(开票日期|开票时间).*(止|结束|截止)|结束日期|截止日期/, /(起|开始|起始)/);
if (!startEl || !endEl) {
  // 没有起止字样时，取「开票日期」分组里的前两个输入框。
  const grouped = all.filter(el => /开票日期|开票时间/.test(labelOf(el)));
  if (grouped.length >= 2) {
    startEl = startEl || grouped[0];
    endEl = endEl || grouped[1];
  }
}
const fill = (el, value) => {
  if (!el) return false;
  if (el.hasAttribute('readonly')) el.removeAttribute('readonly');
  el.focus();
  Object.getOwnPropertyDescriptor(HTMLInputElement.prototype, 'value').set.call(el, value);
  el.dispatchEvent(new Event('input', { bubbles: true }));
  el.dispatchEvent(new Event('change', { bubbles: true }));
  el.blur();
  return true;
};
return {
  start: fill(startEl, startValue),
  end: fill(endEl, endValue),
  startText: startEl ? labelOf(startEl).trim().slice(0, 60) : '',
  endText: endEl ? labelOf(endEl).trim().slice(0, 60) : ''
};
""");
        }

        /// <summary>查询后回读：日期框里的值、表格总数、当前选中的页签，用来核对表单状态有没有真的提交。</summary>
        public static readonly string InvoiceQueryState = Js("""
const labelOf = (el) => {
  let t = (el.getAttribute('placeholder') || '') + ' ' + (el.getAttribute('aria-label') || '');
  t += ' ' + (el.name || '') + ' ' + (el.id || '');
  const wrap = el.closest('.ant-form-item, .el-form-item, .form-item, .form-group, .input-group, .ivu-form-item, tr, div');
  if (wrap) t += ' ' + textOf(wrap).slice(0, 40);
  return t;
};
const dates = Array.from(document.querySelectorAll('input')).filter(visible)
  .filter(el => /开票日期|开票时间/.test(labelOf(el)))
  .map(el => ({ placeholder: (el.getAttribute('placeholder') || '').slice(0, 30), value: el.value || '' }));
const body = document.body ? document.body.innerText.replace(/\s+/g, ' ') : '';
const total = (body.match(/共\s*(\d+)\s*条/) || [])[1] || '';
const active = Array.from(document.querySelectorAll('.is-active,.active,.selected,[aria-selected=true]'))
  .filter(visible).map(textOf).filter(t => t && t.length <= 10).slice(0, 12);
const tabs = Array.from(document.querySelectorAll('a,button,span,div,li'))
  .filter(visible)
  .filter(el => { const t = textOf(el); return t === '全量' || t === '近24小时'; })
  .slice(0, 8)
  .map(el => ({
    tag: el.tagName,
    cls: (el.className || '').toString().slice(0, 60),
    text: textOf(el),
    parentCls: (el.parentElement ? el.parentElement.className : '').toString().slice(0, 60)
  }));
return { dates, total, active, tabs, text: body.slice(0, 400) };
""");

        /// <summary>
        /// 在发票查询页上给页面自己的 XHR / fetch 再包一层，记下 queryFpjcxx 调用时的**明文**报文。
        /// 税局的加密（Jmbw）与地址签名是页面加载后挂在全局 XMLHttpRequest 上的，
        /// 我们从外层包一层先拿到明文；之后用同一个 XHR 重发，加密和签名仍由页面那层完成。
        /// </summary>
        public static readonly string InstallInvoiceQueryCapture = Js("""
if (window.__kqInvoiceCapture) return true;
const state = window.__kqInvoiceCapture = { hits: [], origin: location.origin };
const isTarget = (url) => typeof url === 'string' && url.indexOf('queryFpjcxx') >= 0;
const record = (url, method, body) => {
  if (state.hits.length >= 20) state.hits.shift();
  state.hits.push({
    url: String(url || ''),
    method: String(method || 'GET').toUpperCase(),
    body: typeof body === 'string' ? body : '',
    bodyType: Object.prototype.toString.call(body)
  });
};
const origFetch = window.fetch;
if (origFetch) {
  window.fetch = function (input, init) {
    try {
      const url = typeof input === 'string' ? input : (input && input.url) || '';
      if (isTarget(url)) record(url, (init && init.method) || (input && input.method) || 'GET', init && init.body);
    } catch (e) {}
    return origFetch.apply(this, arguments);
  };
}
const xhrProto = window.XMLHttpRequest && window.XMLHttpRequest.prototype;
if (xhrProto) {
  const origOpen = xhrProto.open;
  const origSend = xhrProto.send;
  xhrProto.open = function (method, url) {
    try { this.__kqInvoiceUrl = url; this.__kqInvoiceMethod = method; } catch (e) {}
    return origOpen.apply(this, arguments);
  };
  xhrProto.send = function (body) {
    try {
      if (isTarget(this.__kqInvoiceUrl)) record(this.__kqInvoiceUrl, this.__kqInvoiceMethod, body);
    } catch (e) {}
    return origSend.apply(this, arguments);
  };
}
return true;
""");

        /// <summary>读取上面钩子记下的发票查询报文（取最近 20 条，交给 C# 判断明文还是密文）。</summary>
        public static readonly string ReadInvoiceQueryCapture = Js("""
const state = window.__kqInvoiceCapture;
return state ? { origin: state.origin, hits: state.hits.slice(-20) } : { origin: '', hits: [] };
""");

        /// <summary>
        /// 用页面自己的 XHR 重发发票查询报文：明文 body 一进 send 就会被页面那层钩子加密并补签名，
        /// 所以这里不需要复刻 Jmbw。返回状态码与响应文本，由 C# 侧解析。
        /// </summary>
        public static string SendInvoiceQuery(string bodyJson)
        {
            string body = JsonSerializer.Serialize(bodyJson);
            string payload = JsAsync("""
const body = __BODY__;
return await new Promise((resolve) => {
  const xhr = new XMLHttpRequest();
  xhr.open('POST', location.origin + '/szzhzz/qlfpcx/v1/queryFpjcxx', true);
  xhr.setRequestHeader('Content-Type', 'application/json;charset=UTF-8');
  xhr.setRequestHeader('Accept', 'application/json, text/plain, */*');
  xhr.withCredentials = true;
  xhr.timeout = 30000;
  xhr.onload = () => resolve({ status: xhr.status, text: xhr.responseText || '' });
  xhr.onerror = () => resolve({ status: 0, text: '', error: 'network' });
  xhr.ontimeout = () => resolve({ status: 0, text: '', error: 'timeout' });
  xhr.send(body);
});
""");
            return payload.Replace("__BODY__", body);
        }

        /// <summary>
        /// 菜单文案是否已经渲染出来。点菜单前用它等页面稳定：SSO 新标签页会先停在「加载中」，
        /// 过早点击会把「还在加载」误判成「没有这个菜单」。
        /// </summary>
        public static string HasMenuText(string text)
        {
            string textJson = JsonSerializer.Serialize(text);
            return Js("const wanted = " + textJson + ";\n" + """
const nodes = Array.from(document.querySelectorAll('a,button,span,div,li')).filter(visible);
return nodes.some(el => {
  const t = textOf(el);
  return t === wanted || (t.indexOf(wanted) >= 0 && t.length <= wanted.length + 8);
});
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

        // ===== 自然人电子税务局（个税，etax.chinatax.gov.cn）=====

        /// <summary>
        /// 个税站点的阶段判定。注意不能沿用企业门户的 Stage 脚本：
        /// 个税门户在未登录的首页也会渲染「我要办税」等导航，按它判成功会假阳性；
        /// 这里要求出现「退出登录/个人中心/单位办税」且不再有「密码登录/注册」。
        /// 短信这一路有两种形状：账号密码提交后的「安全验证」弹窗（占位符是「请输入」），
        /// 以及登录页自带的「短信验证码」输入框，都算需要人工填码。
        /// </summary>
        public static readonly string IitStage = Js("""
const body = document.body ? document.body.innerText : '';
const hasPassword = inputs().some(i => i.type === 'password');
const loggedIn = !hasPassword
  && /(退出登录|退出|个人中心|单位办税)/.test(body)
  && !/(密码登录|立即注册|扫码登录|请登录)/.test(body);
// 「安全验证」是预渲染的隐藏弹窗（.login-security-dialog），打开后才有验证码输入框；
// 登录面板里还常驻着「验证失败请重新滑动验证」这类模板文案，所以一律按可见弹窗的结构判，不按文案判。
const dialogs = Array.from(document.querySelectorAll('.login-security-dialog,.el-dialog,.ant-modal,[role=dialog]')).filter(visible);
const security = dialogs.find(d => /安全验证|验证码|短信/.test(textOf(d)))
  || dialogs.find(d => inputsIn(d).some(i => i.type !== 'password' && /验证码|动态码|短信/.test(describe(i))));
const stray = inputs().find(i => i.type !== 'password' && /验证码|动态码/.test(describe(i)));
return { success: loggedIn, sms: !!security || !!stray, url: location.href };
""");

        /// <summary>
        /// 提交后的页面实况：把弹窗节点（可见的和「存在但被隐藏」的）、可见输入框原样吐回日志。
        /// 等不到「安全验证」时用它分清三种情况：节点根本没出现、出现了但被隐藏、出现了但选择器没覆盖。
        /// </summary>
        public static readonly string IitWatch = Js("""
const all = Array.from(document.querySelectorAll('.login-security-dialog,.el-dialog,.ant-modal,[role=dialog]'))
  .map(d => ({ cls: String(d.className).slice(0, 50), vis: visible(d), txt: textOf(d).replace(/\s+/g, ' ').slice(0, 30) }));
const box = Array.from(document.querySelectorAll('.login-security-dialog,.el-dialog,.ant-modal,[role=dialog]')).filter(visible)[0];
const hidden = all.filter(d => !d.vis).map(d => d.cls.split(' ')[0] || '(无类名)').slice(0, 6);
const inputsSeen = inputs().slice(0, 8).map(i => (i.type || 'text') + ':' + (i.placeholder || '').slice(0, 12));
// 提示条只活 3 秒，提交被拒的原因几乎全在这里；滑块文案则能分清是滑块没过还是账号密码被拒。
const tipEl = document.querySelector('#aliyunCaptcha-sliding-text-box');
const msgs = Array.from(document.querySelectorAll('.el-message,.el-message__content,.el-form-item__error,.el-notification__content,[class*=message]'))
  .filter(visible).map(textOf).filter(t => t.length > 0 && t.length < 60);
return {
  url: location.href.split('?')[0],
  pwd: inputs().some(i => i.type === 'password'),
  tip: tipEl ? textOf(tipEl) : '',
  dialogs: all.filter(d => d.vis && d.txt),
  hidden: hidden,
  inputs: inputsSeen,
  msgs: Array.from(new Set(msgs)).slice(0, 4)
};
""");

        /// <summary>
        /// 个税页面「能点的东西」实况：可见可点元素的文案、路由表（a[href]）、输入框、以及税局自己给的空态提示。
        /// 导航到某个菜单失败时靠它分清是入口文案变了、内容开到了新标签页，还是这个账号本来就没有该单位。
        /// links 把隐藏的菜单项也列出来：税局顶部导航是悬停才展开的下拉，只看可见节点会以为菜单不存在。
        /// </summary>
        public static readonly string IitApps = Js("""
const texts = Array.from(document.querySelectorAll('a,button,[role=button],li,span,div,td'))
  .filter(visible).map(textOf).filter(t => t.length > 0 && t.length <= 24);
const uniq = Array.from(new Set(texts));
const links = Array.from(document.querySelectorAll('a[href]'))
  .map(el => ({ h: (el.getAttribute('href') || '').slice(0, 70), t: textOf(el).slice(0, 20), v: visible(el) ? 1 : 0 }))
  .filter(x => x.t.length > 0 || x.h.indexOf('#') >= 0);
// 税局工作台的菜单不是 a[href]，全是 javascript:void(0) + 前端路由，所以直接全文搜目标菜单名：
// 搜得到说明还藏在没展开的下拉里，搜不到说明这个页面根本没有该菜单，得先跳到别的页面。
const deep = Array.from(document.querySelectorAll('body *'))
  .map(el => ({ t: textOf(el), v: visible(el) ? 1 : 0, g: el.tagName.toLowerCase() + '.' + String(el.className).split(' ')[0].slice(0, 24) }))
  .filter(x => x.t.length > 0 && x.t.length <= 26 && /人员信息采集|专项附加扣除信息采集|工资薪金|所得项目|月内申报/.test(x.t))
  .slice(0, 12);
return {
  url: location.href.split('?')[0],
  hash: location.hash.slice(0, 40),
  hits: uniq.filter(t => /办税|单位|扣缴|人员|采集|申报|查询|确定|进入/.test(t)).slice(0, 24),
  all: uniq.slice(0, 20),
  links: links.slice(0, 40),
  deep,
  // 按钮单独列一份：all 是按出现顺序截断的，工具栏第二个「查询」（筛选面板里那个）会被挤掉。
  btns: Array.from(new Set(Array.from(document.querySelectorAll('button,a,[role=button],.el-button,.el-radio-group label'))
    .filter(visible).map(textOf).filter(t => t.length > 0 && t.length <= 14))).slice(0, 30),
  // 表格有没有行、行里有什么：判断是「没查到数据」还是「查到了但没解析出来」。
  rows: Array.from(document.querySelectorAll('table tbody tr, .el-table__row')).slice(0, 4)
    .map(tr => textOf(tr).slice(0, 60)),
  cols: Array.from(document.querySelectorAll('.el-table__header th, table thead th')).filter(visible)
    .map(th => textOf(th)).filter(t => t.length > 0 && t.length <= 12).slice(0, 20),
  empty: uniq.filter(t => /未检索|无数据|暂无|没有|不存在/.test(t)).slice(0, 6),
  inputs: inputs().slice(0, 6).map(i => (i.type || 'text') + ':' + (i.placeholder || '').slice(0, 12))
};
""");

        /// <summary>
        /// 点扣缴页面的「查询」。人员信息采集工具栏上那个「查询」只是筛选面板的展开开关（点开之后文案变成「收起」），
        /// 真正发列表请求的是面板里跟着输入框的那颗，所以要按「附近有没有输入框」分别挑。
        /// </summary>
        /// <param name="submit">true 只认面板里的（发请求），false 只认工具栏上的（展开面板）。</param>
        public static string IitClickQuery(bool submit)
        {
            return Js("const submit = " + (submit ? "true" : "false") + ";\n" + """
const nearInput = (el) => {
  let p = el;
  for (let i = 0; i < 5 && p; i++) {
    if (p.querySelector && p.querySelector('input')) return true;
    p = p.parentElement;
  }
  return false;
};
const nodes = Array.from(document.querySelectorAll('button,.el-button,a,span,div,li'))
  .filter(el => visible(el) && textOf(el) === '查询');
const hit = submit
  ? nodes.filter(nearInput).reverse()[0] || nodes[nodes.length - 1]
  : nodes.filter(el => !nearInput(el))[0];
if (!hit) return { clicked: false, cands: nodes.length };
const fire = (type) => hit.dispatchEvent(new MouseEvent(type, { bubbles: true, cancelable: true, view: window }));
fire('mouseenter');
fire('mousedown');
fire('mouseup');
hit.click();
return {
  clicked: true,
  inForm: nearInput(hit) ? 1 : 0,
  tag: hit.tagName.toLowerCase() + '.' + String(hit.className).split(' ')[0].slice(0, 22),
  cands: nodes.length
};
""");
        }

        /// <summary>
        /// 抓页面上 el-table 的表头与数据行，拼成「列名: 单元格文本」的对象数组。
        /// 个税扣缴工作台的接口报文被瑞数改成 dmnHafHu= 密文，网络层拿不到 JSON，
        /// 但页面自己解密后照样把名册渲染出来，所以取数改从表格里读税局给展示的那份。
        /// </summary>
        public static readonly string IitTable = Js("""
// Element UI 的固定列会克隆一份表头和表体（.el-table__fixed-*），只认主表那一份，否则列名和行都会翻倍。
const colsOf = (t) => {
  let th = Array.from(t.querySelectorAll('.el-table__header-wrapper thead th'));
  if (th.length === 0) th = Array.from(t.querySelectorAll('thead th'));
  // 空的表头（勾选列）要留着占位，否则列名会整体往前挪一格，姓名落到序号上。
  return th.map(el => textOf(el));
};
const rowsOf = (t) => {
  let tr = Array.from(t.querySelectorAll('.el-table__body-wrapper tbody tr'));
  if (tr.length === 0) tr = Array.from(t.querySelectorAll('tbody tr'));
  return tr.slice(0, 200);
};
const cellsOf = (tr) => Array.from(tr.querySelectorAll('td')).map(td => textOf(td).slice(0, 60));
const zip = (cols, cells) => {
  const o = {};
  const n = Math.min(cols.length, cells.length);
  for (let i = 0; i < n; i++) { if (cols[i].length > 0 && cells[i].length > 0) o[cols[i]] = cells[i]; }
  return o;
};
// 树形表（.el-table__row--level-N）的第一列两用的：父行是「2025年10月」这类所属期，子行是申报表名。
// 照列名映射会把两者搅在一起，所以压平成「税款所属期 + 申报表」两个键；父行只是合计，有子行时丢掉。
const levelOf = (tr) => {
  const m = /el-table__row--level-(\d+)/.exec(String(tr.className));
  return m ? Number(m[1]) : -1;
};
const out = [];
for (const t of Array.from(document.querySelectorAll('.el-table, table')).filter(visible)) {
  const cols = colsOf(t);
  if (cols.some(c => c.length > 0) === false) continue;
  const trs = rowsOf(t);
  let rows;
  if (trs.some(tr => levelOf(tr) >= 0)) {
    rows = [];
    let group = null;
    const flush = () => { if (group && !group.hasChild) rows.push(group.row); group = null; };
    for (const tr of trs) {
      const cells = cellsOf(tr);
      if (cells.some(c => c.length > 0) === false) continue;
      const rest = zip(cols.slice(1), cells.slice(1));
      if (levelOf(tr) <= 0) {
        flush();
        group = { period: cells[0], hasChild: false, row: Object.assign({ '税款所属期': cells[0] }, rest) };
      } else if (group) {
        group.hasChild = true;
        rows.push(Object.assign({ '税款所属期': group.period, '申报表': cells[0] }, rest));
      } else {
        rows.push(Object.assign({ '申报表': cells[0] }, rest));
      }
    }
    flush();
  } else {
    rows = trs.map(tr => zip(cols, cellsOf(tr))).filter(o => Object.keys(o).length > 0);
  }
  rows = rows.filter(r => Object.keys(r).length > 0).slice(0, 200);
  if (rows.length > 0) out.push({ cols: cols.filter(c => c.length > 0).slice(0, 40), rows: rows });
  if (out.length >= 4) break;
}
return { url: location.href.split('?')[0], hash: location.hash.slice(0, 40), tables: out };
""");

        /// <summary>
        /// 税局列表的分页器：click=false 只读状态，true 点一次「下一页」。
        /// 税局表格走 Element UI 分页、表体只渲染当前页，取数要逐页翻；
        /// 页面上可能有多张表，只认「带分页器且行数最多」那张主表的分页器。
        /// </summary>
        public static string IitPager(bool click)
        {
            return Js($"const click = {(click ? "true" : "false")};\n" + """
const colsOf = (t) => {
  let th = Array.from(t.querySelectorAll('.el-table__header-wrapper thead th'));
  if (th.length === 0) th = Array.from(t.querySelectorAll('thead th'));
  return th.map(el => textOf(el));
};
const rowsOf = (t) => {
  let tr = Array.from(t.querySelectorAll('.el-table__body-wrapper tbody tr'));
  if (tr.length === 0) tr = Array.from(t.querySelectorAll('tbody tr'));
  return tr;
};
const pagerOf = (t) => {
  for (let p = t.parentElement, i = 0; p && i < 6; p = p.parentElement, i++) {
    for (const pg of Array.from(p.querySelectorAll('.el-pagination, .ant-pagination')).filter(visible)) {
      // 只认表格后面的分页器，前面那个可能是别的区块的。
      if (t.compareDocumentPosition(pg) & Node.DOCUMENT_POSITION_FOLLOWING) return pg;
    }
  }
  const all = Array.from(document.querySelectorAll('.el-pagination, .ant-pagination')).filter(visible);
  return all.length === 1 ? all[0] : null;
};
const stateOf = (pg) => {
  const active = pg.querySelector('.el-pager li.active, .el-pager li.is-active, .ant-pagination-item-active');
  const next = pg.querySelector('.btn-next, .ant-pagination-next');
  const disabled = !next || next.disabled === true || next.hasAttribute('disabled')
    || /(^|\s)(is-disabled|disabled)(\s|$)/.test(String(next.className));
  return {
    page: active ? parseInt(textOf(active), 10) || 0 : 0,
    next: !!next && !disabled
  };
};
const candidates = Array.from(document.querySelectorAll('.el-table, table'))
  .filter(t => visible(t) && colsOf(t).some(c => c.length > 0) && rowsOf(t).length > 0)
  .map(t => ({ t, pg: pagerOf(t) }))
  .filter(x => x.pg);
if (candidates.length === 0) return { clicked: false, why: 'no-pager' };
// 主表：同页里行数最多的那张，它的分页器才代表列表本身。
candidates.sort((a, b) => rowsOf(b.t).length - rowsOf(a.t).length);
const pg = candidates[0].pg;
const st = stateOf(pg);
const tr = rowsOf(candidates[0].t).find(x => textOf(x).length > 0);
const first = tr ? textOf(tr).slice(0, 40) : '';
if (!click) return { clicked: false, page: st.page, next: st.next, first };
if (!st.next) return { clicked: false, why: 'last', page: st.page, first };
const btn = pg.querySelector('.btn-next, .ant-pagination-next');
const fire = (type) => btn.dispatchEvent(new MouseEvent(type, { bubbles: true, cancelable: true, view: window }));
fire('mouseenter');
fire('mouseover');
fire('mousedown');
fire('mouseup');
btn.click();
return { clicked: true, page: st.page, first };
""");
        }

        /// <summary>
        /// 往「查询统计」那类页面的日期框里填税款所属期。Element 的日期框是 Vue 受控的，
        /// 直接改 value 不会进 model，所以走原生 setter + input/change + 回车，让组件自己按格式解析；
        /// 填没填进去以回读到的值为准，回传细节供日志判断各省的画法。
        /// </summary>
        public static string IitSetPeriod(string start, string end)
        {
            string startJson = JsonSerializer.Serialize(start);
            string endJson = JsonSerializer.Serialize(end);
            return Js($"const wanted = [{startJson}, {endJson}];\n" + """
// 所属期有两种画法：一个区间选择器（两个 .el-range-input）或两个独立日期框（.el-date-editor 里的 .el-input__inner）。
const range = Array.from(document.querySelectorAll('.el-date-editor .el-range-input')).filter(visible);
const single = Array.from(document.querySelectorAll('.el-date-editor .el-input__inner')).filter(visible);
const boxes = range.length >= 2 ? range : single;
if (boxes.length === 0)
  return { clicked: false, editors: document.querySelectorAll('.el-date-editor').length };
const write = Object.getOwnPropertyDescriptor(HTMLInputElement.prototype, 'value').set;
const detail = [];
boxes.slice(0, 2).forEach((el, i) => {
  const before = el.value;
  el.dispatchEvent(new MouseEvent('mousedown', { bubbles: true, cancelable: true, view: window }));
  el.focus();
  write.call(el, wanted[i]);
  el.dispatchEvent(new Event('input', { bubbles: true }));
  el.dispatchEvent(new Event('change', { bubbles: true }));
  for (const type of ['keydown', 'keypress', 'keyup'])
    el.dispatchEvent(new KeyboardEvent(type, { key: 'Enter', code: 'Enter', keyCode: 13, which: 13, bubbles: true }));
  detail.push({ i, ph: (el.placeholder || '').slice(0, 16), before: before.slice(0, 20), after: el.value.slice(0, 20) });
});
return { clicked: true, count: boxes.length, kind: range.length >= 2 ? 'range' : 'single', detail };
""");
        }

        /// <summary>
        /// 缴税记录查询的筛选列是一个 el-select（缴税日期 / 税款所属期）。点开它，下一步再点选项；
        /// 当前已经是目标列时直接返回，不做多余点击。页面上若还有别的下拉，按「当前值像日期/所属期」挑那个。
        /// </summary>
        public static string IitChooseFilter(string label)
        {
            string labelJson = JsonSerializer.Serialize(label);
            return Js("const wanted = " + labelJson + ";\n" + """
const current = (el) => {
  const input = el.querySelector('input');
  return (((input && input.value) || textOf(el)) || '').replace(/\s+/g, '');
};
const selects = Array.from(document.querySelectorAll('.el-select')).filter(visible);
const values = selects.map(current).filter(Boolean).slice(0, 8);
const hit = selects.find(el => current(el) === wanted);
if (hit) return { clicked: true, already: true, current: current(hit), values: values };
const looksLikeDate = (v) => /日期|所属期|期间/.test(v);
const open = selects.find(el => looksLikeDate(current(el))) || selects[0];
if (!open) return { clicked: false, cands: 0, values: values };
const fire = (type) => open.dispatchEvent(new MouseEvent(type, { bubbles: true, cancelable: true, view: window }));
fire('mouseenter');
fire('mousedown');
fire('mouseup');
open.click();
const input = open.querySelector('input');
if (input) {
  input.dispatchEvent(new MouseEvent('mousedown', { bubbles: true, cancelable: true, view: window }));
  input.focus();
  input.click();
}
return { clicked: true, already: false, current: current(open), values: values };
""");
        }

        /// <summary>点 el-select 展开后的某个选项（配 <see cref="IitChooseFilter"/> 用）。</summary>
        public static string IitClickOption(string label)
        {
            string labelJson = JsonSerializer.Serialize(label);
            return Js("const wanted = " + labelJson + ";\n" + """
const roots = Array.from(document.querySelectorAll(
  '.el-select-dropdown__item, .el-select-dropdown li, .el-popper li'));
const nodes = roots.filter(visible).filter(el => textOf(el) === wanted);
const hit = nodes[0];
if (!hit) return { clicked: false, cands: roots.filter(visible).length };
const fire = (type) => hit.dispatchEvent(new MouseEvent(type, { bubbles: true, cancelable: true, view: window }));
fire('mouseenter');
fire('mousedown');
fire('mouseup');
hit.click();
return {
  clicked: true,
  tag: hit.tagName.toLowerCase() + '.' + String(hit.className).split(' ')[0].slice(0, 24)
};
""");
        }

        /// <summary>
        /// 点扣缴工作台 el-menu 里的菜单项（「人员信息采集」这类）。
        /// 实测这些条目一直在 DOM 里，只是被折叠成隐藏节点（li.el-menu-item 且 visible=false），
        /// 沿用「只找可见节点」的点法永远 miss；跳转是 Vue 挂在 li 上的 click，直接点隐藏项一样生效。
        /// </summary>
        public static string IitClickMenu(string text)
        {
            string textJson = JsonSerializer.Serialize(text);
            return Js("const wanted = " + textJson + ";\n" + """
const nodes = Array.from(document.querySelectorAll('li,a,button,span,div'))
  .filter(el => textOf(el) === wanted);
if (nodes.length === 0) return { clicked: false, cands: 0 };
// 面包屑上也有同名的「人员信息采集」，点了等于原地不动；菜单条目（含折叠隐藏的）优先。
const isMenuItem = (el) => /menu-item/.test(String(el.className));
const shown = nodes.find(el => visible(el) && isMenuItem(el)) || nodes.find(visible);
const hit = shown
  || nodes.find(isMenuItem)
  || nodes.sort((a, b) => a.textContent.length - b.textContent.length)[0];
const fire = (type) => hit.dispatchEvent(new MouseEvent(type, { bubbles: true, cancelable: true, view: window }));
fire('mouseenter');
fire('mouseover');
fire('mousedown');
fire('mouseup');
hit.click();
return {
  clicked: true,
  via: shown ? '可见' : '隐藏',
  tag: hit.tagName.toLowerCase() + '.' + String(hit.className).split(' ')[0].slice(0, 24),
  cands: nodes.length
};
""");
        }

        /// <summary>切到「密码登录」页签：个税首页默认停在扫码登录，密码框虽然在 DOM 里但没显示。</summary>
        public static readonly string IitClickPasswordTab = Js("""
const nodes = Array.from(document.querySelectorAll('[role=tab],div,span,a,li,p,button')).filter(visible);
const hit = nodes.find(el => /^密码登录$/.test(textOf(el)));
if (!hit) return false;
hit.click();
return true;
""");

        /// <summary>
        /// 阿里云滑块的几何与状态。把手 #aliyunCaptcha-sliding-slider、轨道 #aliyunCaptcha-sliding-body，
        /// 拖完看 tip 是否为「验证通过」；「请先拖动滑块验证」是未拖时的静态文案，不能当判据。
        /// </summary>
        public static readonly string IitSliderState = Js("""
const body = document.querySelector('#aliyunCaptcha-sliding-body');
const slider = document.querySelector('#aliyunCaptcha-sliding-slider');
const rect = (el) => {
  if (!el || !visible(el)) return null;
  const r = el.getBoundingClientRect();
  return { x: r.x, y: r.y, w: r.width, h: r.height };
};
const tip = (sel) => {
  const el = document.querySelector(sel);
  return el ? textOf(el) : '';
};
return {
  present: !!rect(body) && !!rect(slider),
  body: rect(body),
  slider: rect(slider),
  tip: tip('#aliyunCaptcha-sliding-text-box'),
  passed: /验证通过/.test(tip('#aliyunCaptcha-sliding-text-box')),
  // 税局会在客户端「验证通过」之后再判一次，判砸了就把滑块打回「请按住滑块，拖动到最右边」（旁边标红
  // 「验证失败,请重新滑动验证」）。这时候点提交表单自己就拦住了，一个请求都发不出去，所以不能只认那一瞬的通过。
  // 只认「打回原样」这个位置信号：红色文案会残留在页面上，用它当判据会把后面真的通过的滑块误判成失败。
  reset: /请按住滑块/.test(tip('#aliyunCaptcha-sliding-text-box'))
};
""");

        /// <summary>
        /// 数页面上还有多少串打星号的脱敏值（证件号码 3412************97 这种）。
        /// 明文开关点完用它判断有没有真生效，别把「点了但页面没变」报成成功。
        /// </summary>
        public static readonly string IitMaskedCells = Js("""
const mask = /\*{4,}/;
// 按「谁直接持有这段文字」来数，不按叶子节点数：单元格 div.cell 里常常还挂一个眼睛图标，
// 那时它 children 不为 0，整列号码会被漏掉；同一段文字只会有一个父节点，也不会重复议数。
const owners = Array.from(document.querySelectorAll('td,span,div,p,i,b,label,a'))
  .filter(el => visible(el) && Array.from(el.childNodes).some(n => n.nodeType === 3 && mask.test(n.textContent)));
// 编辑态的单元格可能把号码放在 input.value 里，那里没有文字节点，单独再数一遍。
const fields = Array.from(document.querySelectorAll('input,textarea')).filter(el => mask.test(el.value || ''));
const hits = owners.concat(fields);
const sample = hits.slice(0, 3).map(el => (el.value || el.textContent || '').replace(/\s+/g, '').slice(0, 32));
// rows/idCol 用来判断名册表格画出来没有：点了菜单到数据上屏有几百毫秒到几秒的空档，
// 这段时间里 masked=0 是「还没渲染」，不是「本来就明文」；idCol 认表头，免得把工作台里别的表格当成名册。
const rows = Array.from(document.querySelectorAll('tbody tr')).filter(tr => visible(tr) && textOf(tr).length > 0);
const idCol = Array.from(document.querySelectorAll('th,td')).filter(el => /证件号码|身份证号/.test(textOf(el))).length;
return { masked: hits.length, rows: rows.length, idCol, sample };
""");

        /// <summary>
        /// 点「敏感信息明文」开关。各省叫法不一（显示敏感信息 / 查看明文 / 脱敏），也可能是只带 title 的小眼睛图标，
        /// 所以正文和 title/aria-label 两条路都认；带「隐藏/收起」的先排掉，免得把已经放开的开关又按回去。
        /// 没点着时把页面上的按钮文案一起回传——这个开关长什么样目前只能从实况里学。
        /// </summary>
        public static readonly string IitClickPlain = Js("""
const pool = Array.from(document.querySelectorAll('button,a,span,i,div,label,[role=button]')).filter(visible);
const say = (el) => (textOf(el) + ' ' + (el.title || '') + ' ' + (el.getAttribute('aria-label') || '')).replace(/\s+/g, '');
const hits = pool.filter(el => /明文|敏感|脱敏/.test(say(el)) && !/隐藏|收起|不显示|说明|规则|须知|切换/.test(say(el)) && say(el).length <= 20);
// 工具栏上的真按钮优先，其次才是只带 title 的小眼睛图标；外层容器和图标都会被点着（click 事件冒泡），
// 但排在最前面的那颗最可能是挂处理函数的那个。
const rank = (el) => (/^(BUTTON|A)$/.test(el.tagName) || el.getAttribute('role') === 'button' ? 0 : /^(LABEL|INPUT)$/.test(el.tagName) ? 1 : 2);
const hit = hits.sort((a, b) => rank(a) - rank(b) || say(a).length - say(b).length)[0];
const btns = Array.from(new Set(pool.map(textOf).filter(t => t.length > 0 && t.length <= 12))).slice(0, 24);
if (!hit) return { clicked: false, cands: hits.length, btns };
const fire = (type) => hit.dispatchEvent(new MouseEvent(type, { bubbles: true, cancelable: true, view: window }));
fire('mouseenter');
fire('mousedown');
fire('mouseup');
hit.click();
return {
  clicked: true,
  text: say(hit).slice(0, 24),
  tag: hit.tagName.toLowerCase() + '.' + String(hit.className).split(' ')[0].slice(0, 22),
  cands: hits.length
};
""");

        /// <summary>
        /// 有的省份点完明文开关先压一层「是否查看敏感信息」的二次确认。
        /// 只点弹窗文字自己就在谈明文/敏感的「确定」，其他弹窗一律不碰。
        /// </summary>
        public static readonly string IitConfirmPlain = Js("""
const box = Array.from(document.querySelectorAll('.el-dialog,.el-message-box,.ant-modal,[role=dialog]')).filter(visible)[0];
if (!box) return { clicked: false, reason: 'nodialog' };
const whole = textOf(box);
if (!/明文|敏感|脱敏/.test(whole)) return { clicked: false, reason: whole.slice(0, 40) };
const btns = Array.from(box.querySelectorAll('button,.el-button,[role=button]')).filter(visible);
const hit = btns.find(el => /^(确定|确认|查看|继续|立即验证)$/.test(textOf(el)));
if (!hit) return { clicked: false, reason: 'nobtn:' + btns.map(textOf).join(',').slice(0, 40) };
hit.click();
return { clicked: true, text: textOf(hit) };
""");

        /// <summary>
        /// 点「登录」提交。页面上能匹配到「登录」的不止一处（右上角还有个「注册 登录」链接），
        /// 真正的提交按钮是表单里的 BUTTON（class 含 submit），只点文字命中的 div 不会有反应。
        /// </summary>
        public static readonly string IitClickSubmit = Js("""
const nodes = Array.from(document.querySelectorAll('button, a, div, span')).filter(visible);
const hits = nodes.filter(el => {
  const t = textOf(el);
  return t === '登录' && !/注册|忘记|其他|扫码|切换/.test(t);
});
const hit = hits.find(el => el.tagName === 'BUTTON' || /submit/i.test(String(el.className)))
  || hits[hits.length - 1];
if (!hit) return false;
hit.click();
return true;
""");

        /// <summary>把短信验证码填进「安全验证」弹窗（占位符只有「请输入」），优先取弹窗里的那个框。</summary>
        public static string IitFillSms(string code)
        {
            string codeJson = JsonSerializer.Serialize(code);
            return Js("const code = " + codeJson + ";\n" + """
const box = Array.from(document.querySelectorAll('.login-security-dialog,.el-dialog,.ant-modal,[role=dialog]')).filter(visible)[0];
const pool = (box ? inputsIn(box) : inputs()).filter(i => i.type !== 'password' && !/(图形|图片)/.test(describe(i)));
// 没有可见弹窗时只认明确标着「验证码」的框：逐帧试填时若回落到 pool[0]，会把短信码敲进账号框。
const el = pool.find(i => /验证码|动态码/.test(describe(i))) || (box ? pool[0] : null);
if (!el) return { filled: false, dialog: !!box };
setValue(el, code);
return { filled: true, dialog: !!box, placeholder: el.placeholder || '' };
""");
        }

        /// <summary>点「安全验证」弹窗的提交按钮：先在可见弹窗里按文案找，再退回弹窗内的主色按钮。</summary>
        public static readonly string IitClickSmsConfirm = Js("""
const box = Array.from(document.querySelectorAll('.login-security-dialog,.el-dialog,.ant-modal,[role=dialog]')).filter(visible)[0];
const label = /^(验证|确定|提交|下一步|立即验证)$/;
// 只点弹窗里的按钮：没有弹窗时回落到整页找 primary 会命中登录页的「登录」，一次点击就是一条短信。
if (box) {
  const btns = Array.from(box.querySelectorAll('button, a, [role=button]')).filter(visible);
  const hit = btns.find(el => label.test(textOf(el)))
    || btns.find(el => /el-button--primary/.test((el.className || '').toString()));
  if (!hit) return false;
  hit.click();
  return true;
}
// 弹窗不是标准 dialog 节点时，退一步只点文字明确是「验证/确定/提交」的按钮，并排除表单提交按钮。
const loose = Array.from(document.querySelectorAll('button, a, [role=button]')).filter(visible)
  .filter(el => label.test(textOf(el)) && !/submit/i.test(String(el.className)));
if (loose.length === 0) return false;
loose[0].click();
return true;
""");


        /// <summary>切到「个人所得税APP」扫码登录页签。</summary>
        public static readonly string IitClickQrTab = Js("""
const nodes = Array.from(document.querySelectorAll('[role=tab],div,span,a,li,p')).filter(visible);
const hit = nodes.find(el => /个人所得税APP|扫码登录/.test(textOf(el)) && textOf(el).length <= 16);
if (!hit) return false;
hit.click();
return true;
""");

        /// <summary>读扫码登录的二维码：登录面板里那张 data URL 的近方形大图。</summary>
        public static readonly string IitQrImage = Js("""
const imgs = Array.from(document.querySelectorAll('img')).filter(visible);
const qr = imgs.find(el => {
  const src = el.currentSrc || el.src || '';
  const r = el.getBoundingClientRect();
  const scope = (el.closest('[class*=qrcode]') || el.parentElement || {}).className || '';
  return /^data:image/i.test(src) && r.width >= 90 && r.width <= 340 && Math.abs(r.width - r.height) < 24
    && /qrcode/i.test(String(scope));
});
const any = qr || imgs.find(el => {
  const src = el.currentSrc || el.src || '';
  const r = el.getBoundingClientRect();
  return /^data:image/i.test(src) && r.width >= 100 && r.width <= 340 && Math.abs(r.width - r.height) < 24;
});
return { present: !!any, image: any ? (any.currentSrc || any.src || '') : '' };
""");

        /// <summary>二维码是否已失效（页面上出现失效/刷新提示）。</summary>
        public static readonly string IitQrExpired = Js("""
const body = document.body ? document.body.innerText : '';
return /(二维码已失效|二维码过期|二维码失效|请点击刷新|刷新二维码)/.test(body);
""");

        /// <summary>点二维码刷新按钮。</summary>
        public static readonly string IitClickQrRefresh = Js("""
const nodes = Array.from(document.querySelectorAll('div,span,a,button')).filter(visible);
const hit = nodes.find(el => /^(刷新|点击刷新|刷新二维码|重新获取)$/.test(textOf(el)));
if (!hit) return false;
hit.click();
return true;
""");

        /// <summary>
        /// 摸底用：在个税（ITS）页面里找它自己的加解密入口——列全局加密对象、扫同源 JS 源码里的关键字、
        /// 试探 webpack 模块表。个税接口响应是整包密文，但加解密就在这一份 JS 上下文里做，
        /// 拿到函数位置就不必自己复刻算法。返回值是纯诊断信息，不做任何业务请求。
        /// </summary>
        public static readonly string IitCryptoRecon = JsAsync("""
const markers = ['dmnHafHu', 'getPublicKey', 'tokenKey', 'Jmbw', 'encrypt', 'decrypt', 'CryptoJS', 'JSEncrypt', 'SM2', 'SM4', 'AES'];
const ctxOf = (src, m) => {
  const out = [];
  let i = src.indexOf(m);
  while (i >= 0 && out.length < 2) {
    out.push(i + '｜' + src.slice(Math.max(0, i - 100), i + 180).replace(/\s+/g, ' '));
    i = src.indexOf(m, i + m.length + 500);
  }
  return out;
};
const scan = (src) => {
  const hits = {};
  for (const m of markers) { const c = ctxOf(src, m); if (c.length > 0) hits[m] = c; }
  return hits;
};
const typeOfGlobal = {};
for (const n of ['CryptoJS', 'JSEncrypt', 'sm2', 'sm4', 'axios', '__axios', 'webUp', 'UA', '$_ts']) {
  try { typeOfGlobal[n] = typeof window[n]; } catch (e) { typeOfGlobal[n] = 'error'; }
}
const fnNames = (o) => { try { return Object.keys(o).filter(k => typeof o[k] === 'function').slice(0, 24); } catch (e) { return []; } };
const crypto = { subtle: !!(window.crypto && window.crypto.subtle), globals: {} };
try {
  if (window.CryptoJS) crypto.globals.CryptoJS = { aes: fnNames(window.CryptoJS.AES), enc: fnNames(window.CryptoJS.enc) };
  if (window.JSEncrypt) crypto.globals.JSEncrypt = fnNames(window.JSEncrypt.prototype);
} catch (e) { crypto.globalsError = String(e).slice(0, 160); }
const scripts = [];
try {
  const list = Array.from(document.querySelectorAll('script[src]')).map(s => s.src);
  let fetched = 0;
  for (const src of list) {
    if (fetched >= 10) break;
    try {
      const u = new URL(src, location.href);
      if (u.origin !== location.origin) { scripts.push({ src: src.slice(0, 160), same: false }); continue; }
      const res = await fetch(u.href);
      const text = await res.text();
      fetched++;
      const hits = scan(text);
      if (Object.keys(hits).length > 0) scripts.push({ src: src.slice(0, 160), same: true, len: text.length, hits });
    } catch (e) { scripts.push({ src: src.slice(0, 160), error: String(e).slice(0, 120) }); }
  }
} catch (e) { scripts.push({ listError: String(e).slice(0, 120) }); }
const inline = [];
try {
  for (const s of Array.from(document.querySelectorAll('script:not([src])'))) {
    const hits = scan(s.textContent || '');
    if (Object.keys(hits).length > 0) inline.push({ len: (s.textContent || '').length, hits });
  }
} catch (e) { inline.push({ error: String(e).slice(0, 120) }); }
// webpack 的模块表：把一次性 chunk 推给它注册的 push，回调里能拿到 require，进而拿到全部模块工厂的源码。
let webpack = null;
try {
  const key = Object.keys(window).find(k => /^webpackChunk/i.test(k) || /^webpackJsonp/i.test(k));
  if (key) {
    let req = null;
    const arr = window[key];
    arr.push([['__kj_probe__'], {}, [c => { req = c; }]]);
    if (req && req.m) {
      const ids = Object.keys(req.m);
      const found = [];
      for (const id of ids) {
        if (found.length >= 8) break;
        try {
          const src = String(req.m[id]);
          if (src.indexOf('dmnHafHu') >= 0 || src.indexOf('decrypt') >= 0 || src.indexOf('getPublicKey') >= 0) {
            found.push({ id, hits: scan(src) });
          }
        } catch (e) { }
      }
      webpack = { global: key, modules: ids.length, found };
    } else {
      webpack = { global: key, requireUnavailable: true };
    }
  }
} catch (e) { webpack = { error: String(e).slice(0, 160) }; }
return {
  href: location.href.slice(0, 200),
  isTop: window === window.top,
  typeOfGlobal,
  crypto,
  webpack,
  scripts,
  inline
};
""");

        /// <summary>
        /// 明文抓手：瑞数与个税应用层把响应整包加密，但页面画表格之前必然已经把密文解成明文再交给
        /// <c>JSON.parse</c>。在 <c>JSON.parse</c> 外面包一层就能拿到解密后的报文，不碰算法、不碰密钥。
        /// 同时给 XHR / fetch 标上「最近完成的请求地址」，好把每份明文对回它的接口。每次装都清空缓冲：
        /// 取数前都会先装一次，留着上一轮的明文会把旧数据混进这一次的解析结果。
        /// </summary>
        public static readonly string IitPlainHook = Js("""
if (window.__kjPlain) { const old = window.__kjPlain; old.rows.length = 0; old.parseCalls = 0; old.big = 0; return { already: true, cleared: true }; }
const P = { rows: [], parseCalls: 0, big: 0 };
window.__kjPlain = P;
const raw = JSON.parse;
const note = (url, text) => {
  P.parseCalls++;
  if (typeof text !== 'string' || text.length < 120) return;
  const c = text.charAt(0);
  if (c !== '{' && c !== '[') return;
  P.big++;
  if (text.length > 60000) return;
  let keys = null;
  let count = null;
  try {
    const v = raw(text);
    if (Array.isArray(v)) { count = v.length; keys = v.length > 0 && v[0] && typeof v[0] === 'object' ? Object.keys(v[0]).slice(0, 40) : []; }
    else if (v && typeof v === 'object') keys = Object.keys(v).slice(0, 40);
  } catch (e) { return; }
  P.rows.push({ url: String(url || '').slice(0, 180), at: Date.now(), len: text.length, count, keys, body: text });
  if (P.rows.length > 12) P.rows.shift();
};
try {
  const XP = XMLHttpRequest.prototype;
  const open = XP.open;
  const send = XP.send;
  XP.open = function (method, url) { try { this.__kjUrl = String(url); } catch (e) { } return open.apply(this, arguments); };
  XP.send = function () {
    try {
      this.addEventListener('load', () => { window.__kjLastUrl = this.__kjUrl; });
    } catch (e) { }
    return send.apply(this, arguments);
  };
} catch (e) { P.hookError = 'xhr:' + String(e).slice(0, 120); }
try {
  if (typeof window.fetch === 'function') {
    const f = window.fetch;
    window.fetch = function (input) {
      try {
        const u = typeof input === 'string' ? input : (input && input.url);
        if (u) window.__kjNextUrl = String(u);
      } catch (e) { }
      return f.apply(this, arguments).then(r => {
        try { window.__kjLastUrl = window.__kjNextUrl || window.__kjLastUrl; } catch (e) { }
        return r;
      });
    };
  }
} catch (e) { P.hookError = (P.hookError || '') + ' fetch:' + String(e).slice(0, 120); }
JSON.parse = function (text) {
  const v = raw.apply(this, arguments);
  try { note(window.__kjLastUrl || '', text); } catch (e) { }
  return v;
};
return { installed: true };
""");

        /// <summary>
        /// 取 <see cref="IitPlainHook" /> 缓冲里的明文报文。<c>fragment</c> 是接口地址片段（如 <c>queryRyxxcjList</c>）：
        /// 给了就只回匹配那几条并带上完整正文（交给按候选字段名取值的解析器），留空则回全部条目的概况，用于摸底。
        /// </summary>
        public static string IitPlainTake(string fragment)
        {
            var needleJson = JsonSerializer.Serialize(fragment ?? string.Empty);
            return Js("const needle = " + needleJson + ".toLowerCase();\n" + """
const P = window.__kjPlain;
if (!P) return { missing: true };
const hit = (r) => {
  const u = String(r.url || '').toLowerCase();
  if (needle.length === 0) return true;
  if (u.indexOf(needle) >= 0) return true;
  // 页面自己的伪地址（hash / #table）带不上接口名，退而认正文里的字段名。
  const keys = r.keys || [];
  return keys.length === 0 ? false : String(keys).toLowerCase().indexOf(needle) >= 0;
};
const rows = P.rows.filter(hit);
if (needle.length === 0) {
  return {
    parseCalls: P.parseCalls,
    big: P.big,
    hookError: P.hookError || null,
    lastUrl: String(window.__kjLastUrl || '').slice(0, 180),
    rows: P.rows.slice(-14).map(r => ({ url: r.url, len: r.len, count: r.count, keys: r.keys, head: String(r.body || '').slice(0, 900) }))
  };
}
return {
  parseCalls: P.parseCalls,
  big: P.big,
  matched: rows.length,
  rows: rows.map(r => ({ url: r.url, len: r.len, body: r.body }))
};
""");
        }

        /// <summary>
        /// 包成「立即执行」表达式：上下文级初始化脚本只把源码当表达式求值，
        /// 直接塞一个箭头函数进去等于什么都没跑，所以要自己调用一次。
        /// </summary>
        public static string AsInvoke(string script)
        {
            return "(" + script + ")()";
        }

        /// <summary>
        /// 包一层返回值归一化：Playwright 的 EvaluateAsync&lt;string&gt; 只接受字符串，
        /// 脚本里直接 return 对象会在反序列化时失败，所以统一 stringify 后再回传。
        /// </summary>
        public static string AsJson(string script)
        {
            return "() => { const r = (" + script + ")(); return typeof r === 'string' ? r : JSON.stringify(r); }";
        }

        /// <summary>异步脚本版：等 Promise 结果再序列化，供页面里临时发请求使用。</summary>
        public static string AsJsonAsync(string script)
        {
            return "async () => { const r = await (" + script + ")(); return typeof r === 'string' ? r : JSON.stringify(r); }";
        }

        private static string Js(string body)
        {
            return "() => {\n" + Helpers + "\n" + body + "\n}";
        }

        private static string JsAsync(string body)
        {
            return "async () => {\n" + Helpers + "\n" + body + "\n}";
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
const inputsIn = (root) => Array.from((root || document).querySelectorAll('input')).filter(visible).filter(el => !el.disabled && !el.readOnly);
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
