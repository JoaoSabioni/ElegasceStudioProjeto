import Link from 'next/link'
import type { Metadata } from 'next'

export const metadata: Metadata = {
  title: 'Politica de Privacidade | Elegance Studio',
  description: 'Politica de Privacidade do Elegance Studio - Barbearia em Pinhal Novo.',
}

export default function PoliticaPrivacidade() {
  return (
    <div className="bg-black text-white font-sans min-h-screen px-6 md:px-8 py-20 md:py-32">
      <div className="max-w-3xl mx-auto">
        <Link href="/main" className="text-[10px] tracking-[0.4em] text-zinc-600 uppercase hover:text-zinc-400 transition-colors mb-16 inline-block">
          Voltar ao inicio
        </Link>

        <h1 className="font-serif text-[clamp(2rem,6vw,64px)] uppercase tracking-tighter leading-tight mb-4">
          Politica de<br />Privacidade
        </h1>
        <p className="text-[11px] tracking-[0.3em] text-zinc-600 uppercase mb-16">
          Ultima atualizacao: 21 maio 2026
        </p>

        <div className="space-y-12 text-[13px] text-zinc-400 leading-relaxed">
          <section>
            <h2 className="font-serif text-xl uppercase tracking-tight text-white mb-4">1. Responsavel pelo tratamento</h2>
            <p>
              <strong className="text-zinc-200">Elegance Studio</strong><br />
              Pinhal Novo, Portugal.
            </p>
          </section>

          <section>
            <h2 className="font-serif text-xl uppercase tracking-tight text-white mb-4">2. Dados recolhidos</h2>
            <p>
              Quando faz uma marcacao online, recolhemos os dados necessarios para gerir o agendamento:
              nome, numero de telemovel, barbeiro escolhido, servico(s), data, hora e estado da marcacao.
            </p>
            <p className="mt-4">
              Estes dados sao usados apenas para confirmar, gerir, reagendar, cancelar e comunicar informacao relacionada com a marcacao.
            </p>
          </section>

          <section>
            <h2 className="font-serif text-xl uppercase tracking-tight text-white mb-4">3. Base legal</h2>
            <p>
              O tratamento e necessario para prestar o servico pedido pelo cliente e para comunicacoes operacionais relacionadas com a marcacao.
            </p>
          </section>

          <section>
            <h2 className="font-serif text-xl uppercase tracking-tight text-white mb-4">4. Conservacao</h2>
            <p>
              As marcacoes podem ser mantidas pelo periodo necessario para gestao operacional, historico interno e cumprimento de obrigacoes legais.
              Dados que ja nao sejam necessarios devem ser eliminados ou anonimizados.
            </p>
          </section>

          <section>
            <h2 className="font-serif text-xl uppercase tracking-tight text-white mb-4">5. Subcontratantes e plataformas externas</h2>
            <p>
              A aplicacao pode usar fornecedores de alojamento, base de dados, mensagens SMS/WhatsApp ou email.
              Esses fornecedores devem tratar os dados apenas para prestar o servico tecnico necessario.
            </p>
          </section>

          <section>
            <h2 className="font-serif text-xl uppercase tracking-tight text-white mb-4">6. Direitos do utilizador</h2>
            <p>
              Pode solicitar acesso, correcao, apagamento ou limitacao do tratamento dos seus dados, nos termos do RGPD.
              Para exercer estes direitos, contacte diretamente a equipa da Elegance Studio.
            </p>
          </section>

          <section>
            <h2 className="font-serif text-xl uppercase tracking-tight text-white mb-4">7. Cookies</h2>
            <p>
              O site nao usa cookies de publicidade. Podem existir cookies ou armazenamento tecnico estritamente necessario ao funcionamento da aplicacao.
            </p>
          </section>

          <section>
            <h2 className="font-serif text-xl uppercase tracking-tight text-white mb-4">8. Autoridade de supervisao</h2>
            <p>
              Em Portugal, pode apresentar reclamacao junto da CNPD - Comissao Nacional de Protecao de Dados.
            </p>
          </section>
        </div>

        <div className="mt-20 pt-10 border-t border-white/5">
          <Link href="/main" className="text-[11px] tracking-[0.4em] text-zinc-600 uppercase hover:text-zinc-400 transition-colors">
            Voltar ao inicio
          </Link>
        </div>
      </div>
    </div>
  )
}
