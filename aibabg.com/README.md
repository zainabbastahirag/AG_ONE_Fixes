# AI Baba G — aibabg.com

Ready-to-deploy starter website for **AI Baba G**.

## What's included

| File | What it is |
|------|------------|
| `index.html` | Homepage with 4 tool cards |
| `scam-check.html` | **Working scam checker** (offline rules engine) |
| `write.html` | Letter writer with 8 templates |
| `reply.html` | WhatsApp reply helper (3 styles) |
| `explains.html` | Explains index page |
| `scam/bank-sms.html` | SEO landing page example |
| `explains/chatgpt.html` | SEO explains page example |
| `write/resignation.html` | SEO letter page example |
| `SITEMAP.md` | 50 URLs with titles + meta descriptions |
| `IDEAS-30.md` | 30 business ideas table |
| `css/style.css` | Full styling |

## Preview locally

```bash
cd aibabg.com
python3 -m http.server 8080
# Open http://localhost:8080
```

## Deploy to aibabg.com

### Option 1: Netlify (free, easiest)
1. Drag the `aibabg.com` folder to https://app.netlify.com/drop
2. Point your domain DNS to Netlify

### Option 2: Azure Static Web Apps
```bash
az staticwebapp create --name aibabg --source .
```

### Option 3: Any web host
Upload all files via FTP to your hosting `public_html` folder.

## DNS setup for aibabg.com

| Type | Name | Value |
|------|------|-------|
| A | @ | Your host IP (or Netlify/Vercel IP) |
| CNAME | www | aibabg.com |

## Next steps

1. Deploy these files to your domain
2. Add more SEO pages from `SITEMAP.md`
3. Connect OpenAI API for smarter responses
4. Add Google Search Console + submit sitemap
5. Write 2 blog posts per week from SITEMAP.md blog section
