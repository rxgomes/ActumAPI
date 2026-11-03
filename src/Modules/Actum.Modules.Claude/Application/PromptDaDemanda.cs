using System.Globalization;
using System.Security;
using System.Text;
using System.Text.Json;
using Actum.Modules.Claude.Contracts;

namespace Actum.Modules.Claude.Application;

/// <summary>
/// Instructions and context sent to the Claude API to structure a demand, and the JSON Schema of the answer
/// </summary>
internal static class PromptDaDemanda
{
    public const string Sistema = """
        Você ajuda um desenvolvedor de software a organizar as demandas que ele recebe pelo Asana. A partir do
        contexto da tarefa (título, descrição, projetos, comentários e histórico de reaberturas), estruture a
        demanda em quatro partes, sempre em português do Brasil:

        - objetivo: de 1 a 3 frases dizendo qual resultado é esperado quando a tarefa estiver pronta, do ponto
          de vista de quem pediu. Não repita o título.
        - checklist: ações concretas e verificáveis, curtas, que o desenvolvedor consiga marcar como feitas. Use
          apenas o que o contexto pede ou implica diretamente; não invente requisitos.
          - grupo: a área ou camada da ação, com um nome curto tirado do próprio contexto, por exemplo "Front",
            "Back: cálculo", "Back: integração TecnoSpeed", "Banco de dados". Itens do mesmo grupo vêm juntos;
            os grupos e os itens dentro deles seguem a ordem em que devem ser feitos. Se a demanda não tiver
            áreas distintas, use um único grupo "Geral".
          - vago: quando o pedido daquela ação estiver vago ou incompleto no contexto (por exemplo, "ver onde mais
            mexer"), explique em poucas palavras o que está vago; caso contrário, devolva texto vazio.
        - testes: como validar a entrega (cenários a testar e dados de referência citados, como números de nota,
          clientes, filiais ou ambientes). Não repita esses testes no checklist. Se o contexto não indicar
          nada para testar, devolva a lista vazia.
        - duvidas: perguntas a fazer para quem pediu, apenas sobre lacunas, ambiguidades ou contradições reais no
          contexto, de preferência ligadas aos itens marcados como vagos. Se não houver dúvida relevante,
          devolva a lista vazia.

        Os comentários podem trazer pedidos de ajuste, respostas a perguntas anteriores ou decisões que mudam a
        descrição original: o que foi combinado depois prevalece sobre o que veio antes. Ignore mensagens
        automáticas, avisos ("vou avisar quando estiver pronto") e conversas que não alteram o que deve ser feito.
        """;

    /// <summary>
    /// JSON Schema of the structured answer (<c>objetivo</c>, <c>checklist</c>, <c>testes</c>, <c>duvidas</c>)
    /// </summary>
    public static IReadOnlyDictionary<string, JsonElement> Esquema { get; } = new Dictionary<string, JsonElement>
    {
        ["type"] = JsonSerializer.SerializeToElement("object"),
        ["additionalProperties"] = JsonSerializer.SerializeToElement(false),
        ["required"] = JsonSerializer.SerializeToElement(new[] { "objetivo", "checklist", "testes", "duvidas" }),
        ["properties"] = JsonSerializer.SerializeToElement(new
        {
            objetivo = new { type = "string" },
            checklist = new
            {
                type = "array",
                items = new
                {
                    type = "object",
                    additionalProperties = false,
                    required = new[] { "grupo", "texto", "vago" },
                    properties = new
                    {
                        grupo = new { type = "string" },
                        texto = new { type = "string" },
                        vago = new { type = "string" },
                    },
                },
            },
            testes = new { type = "array", items = new { type = "string" } },
            duvidas = new { type = "array", items = new { type = "string" } },
        }),
    };

    /// <summary>
    /// Build the user message with the task context in XML blocks
    /// </summary>
    public static string MontarMensagem(DemandaParaAnalise demanda)
    {
        var texto = new StringBuilder();
        texto.AppendLine("Estruture a demanda da tarefa abaixo.");
        texto.AppendLine();
        texto.AppendLine("<tarefa>");
        Bloco(texto, "empresa", demanda.Empresa);
        Bloco(texto, "titulo", demanda.Titulo);
        Bloco(texto, "projetos", demanda.Projetos);
        Bloco(texto, "descricao", demanda.Descricao);

        texto.AppendLine("<comentarios>");
        foreach (var comentario in demanda.Comentarios)
        {
            texto.Append(CultureInfo.InvariantCulture, $"<comentario autor=\"{Escapar(comentario.Autor ?? "desconhecido")}\" data=\"{Data(comentario.CriadoEm)}\">");
            texto.Append(Escapar(comentario.Texto));
            texto.AppendLine("</comentario>");
        }

        texto.AppendLine("</comentarios>");

        texto.AppendLine("<retrabalhos>");
        foreach (var retrabalho in demanda.Retrabalhos)
        {
            texto.AppendLine(CultureInfo.InvariantCulture, $"<retrabalho numero=\"{retrabalho.Numero}\" reaberta_em=\"{Data(retrabalho.ReabertaEm)}\" reaberta_por=\"{Escapar(retrabalho.ReabertaPor ?? "desconhecido")}\" />");
        }

        texto.AppendLine("</retrabalhos>");
        texto.AppendLine("</tarefa>");

        var ultimo = demanda.Retrabalhos.MaxBy(retrabalho => retrabalho.Numero);
        if (ultimo is not null)
        {
            texto.AppendLine();
            texto.AppendLine(CultureInfo.InvariantCulture, $"""
                A tarefa já foi entregue e reaberta {demanda.Retrabalhos.Count} vez(es); a reabertura mais recente
                (número {ultimo.Numero}) foi em {Data(ultimo.ReabertaEm)}. Priorize o que foi pedido a partir dessa
                reabertura (os comentários dessa data em diante): o checklist deve conter o que ainda falta fazer
                para atender a reabertura, e o objetivo deve refletir o ajuste pedido.
                """);
        }

        return texto.ToString();
    }

    private static void Bloco(StringBuilder texto, string nome, string valor)
    {
        texto.AppendLine(CultureInfo.InvariantCulture, $"<{nome}>{Escapar(valor)}</{nome}>");
    }

    private static string Data(DateTime data) => data.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);

    private static string Escapar(string valor) => SecurityElement.Escape(valor) ?? string.Empty;
}
