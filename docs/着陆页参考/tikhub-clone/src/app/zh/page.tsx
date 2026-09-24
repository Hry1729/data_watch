import { Header } from "@/components/sites/tikhub-io-5f62aaaa/zh-d83abd2e/components/Header";
import { Hero } from "@/components/sites/tikhub-io-5f62aaaa/zh-d83abd2e/components/Hero";
import { PlatformMarquee } from "@/components/sites/tikhub-io-5f62aaaa/zh-d83abd2e/components/PlatformMarquee";
import { StatsStrip } from "@/components/sites/tikhub-io-5f62aaaa/zh-d83abd2e/components/StatsStrip";
import { ApiPreview } from "@/components/sites/tikhub-io-5f62aaaa/zh-d83abd2e/components/ApiPreview";
import { McpIntegration } from "@/components/sites/tikhub-io-5f62aaaa/zh-d83abd2e/components/McpIntegration";
import { Datasets } from "@/components/sites/tikhub-io-5f62aaaa/zh-d83abd2e/components/Datasets";
import { Marketplace } from "@/components/sites/tikhub-io-5f62aaaa/zh-d83abd2e/components/Marketplace";
import { UseCases } from "@/components/sites/tikhub-io-5f62aaaa/zh-d83abd2e/components/UseCases";
import { CtaSection } from "@/components/sites/tikhub-io-5f62aaaa/zh-d83abd2e/components/CtaSection";
import { Footer } from "@/components/sites/tikhub-io-5f62aaaa/zh-d83abd2e/components/Footer";
import { ScrollToTop } from "@/components/sites/tikhub-io-5f62aaaa/zh-d83abd2e/components/ScrollToTop";

export default function ZhPage() {
  return (
    <>
      <Header />
      <main>
        <Hero />
        <PlatformMarquee />
        <StatsStrip />
        <ApiPreview />
        <McpIntegration />
        <Datasets />
        <Marketplace />
        <UseCases />
        <CtaSection />
      </main>
      <Footer />
      <ScrollToTop />
    </>
  );
}
