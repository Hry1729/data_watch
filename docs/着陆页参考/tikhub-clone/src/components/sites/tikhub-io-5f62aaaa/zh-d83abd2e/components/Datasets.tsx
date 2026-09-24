import Image from "next/image";
import { ArrowRightIcon } from "../../shared/icons";

type DatasetTier = {
  records: string;
  startsAt: string;
  perThousand: string;
};

const TIERS: DatasetTier[] = [
  { records: "100K–500K", startsAt: "$150", perThousand: "$2.00" },
  { records: "500K–5M", startsAt: "$600", perThousand: "$1.20" },
  { records: "5M–20M", startsAt: "$3,000", perThousand: "$0.60" },
  { records: "20M+", startsAt: "$8,000", perThousand: "$0.40" },
];

function DatasetImagesStack() {
  const images = [
    {
      src: "/sites/tikhub-io-5f62aaaa/zh-d83abd2e/images/tiktok-video-dataset-export.webp",
      alt: "YouTube 视频数据集",
      rotate: -2,
    },
    {
      src: "/sites/tikhub-io-5f62aaaa/zh-d83abd2e/images/social-profile-dataset-fields.webp",
      alt: "Instagram Post dataset",
      rotate: 1.5,
    },
    {
      src: "/sites/tikhub-io-5f62aaaa/zh-d83abd2e/images/social-engagement-dataset.webp",
      alt: "TikTok Product dataset",
      rotate: -0.5,
    },
  ];
  return (
    <div className="relative h-80 md:h-96">
      {images.map((img, i) => (
        <div
          key={i}
          className="absolute inset-x-0 rounded-2xl overflow-hidden border border-border bg-card shadow-2xl"
          style={{
            transform: `rotate(${img.rotate}deg) translateY(${i * 16}px)`,
            zIndex: images.length - i,
          }}
        >
          <Image
            src={img.src}
            alt={img.alt}
            width={800}
            height={400}
            className="w-full h-auto"
          />
        </div>
      ))}
    </div>
  );
}

function DatasetTableAndLinks() {
  return (
    <div>
      <h3 className="text-2xl font-semibold">浏览我们的数据集</h3>
      <p className="mt-3 text-muted-foreground">
        所有数据集附带 CSV/Parquet 下载,字段文档化,使用条款清晰,商业用途友好。
      </p>
      <div className="mt-6 rounded-2xl border border-border bg-card overflow-hidden">
        <table className="w-full text-sm">
          <thead className="bg-muted text-muted-foreground">
            <tr>
              <th className="px-4 py-3 text-left font-medium">记录数</th>
              <th className="px-4 py-3 text-left font-medium">起价</th>
              <th className="px-4 py-3 text-left font-medium">每千条</th>
            </tr>
          </thead>
          <tbody>
            {TIERS.map((t, i) => (
              <tr key={i} className="border-t border-border">
                <td className="px-4 py-3">{t.records}</td>
                <td className="px-4 py-3 font-medium">{t.startsAt}</td>
                <td className="px-4 py-3 text-muted-foreground">
                  {t.perThousand}
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
      <div className="mt-6 flex flex-wrap gap-3">
        <a
          href="#"
          className="inline-flex items-center gap-2 text-sm font-medium text-accent hover:underline"
        >
          浏览全部数据集 <ArrowRightIcon className="w-4 h-4" />
        </a>
        <a
          href="#"
          className="inline-flex items-center gap-2 text-sm font-medium text-muted-foreground hover:underline"
        >
          申请定制数据集 <ArrowRightIcon className="w-4 h-4" />
        </a>
      </div>
    </div>
  );
}

export function Datasets() {
  return (
    <section className="scroll-mt-20">
      <div className="max-w-7xl mx-auto px-6 py-20 md:py-28">
        <div className="text-center mb-12">
          <p className="text-sm text-muted-foreground">数据集</p>
          <h2 className="mt-2 text-3xl md:text-4xl lg:text-5xl font-bold tracking-tight">
            10亿+ 记录
          </h2>
          <p className="mt-3 text-muted-foreground max-w-2xl mx-auto">
            从 14 个平台收集 2,000 万+ 个数据集,可立即下载用于训练、微调或研究。
          </p>
        </div>
        <div className="grid lg:grid-cols-2 gap-12 items-center">
          <DatasetImagesStack />
          <DatasetTableAndLinks />
        </div>
      </div>
    </section>
  );
}
