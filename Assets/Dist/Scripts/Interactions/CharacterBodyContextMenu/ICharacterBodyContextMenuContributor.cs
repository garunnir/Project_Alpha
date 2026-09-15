// ============================================================
// ICharacterBodyContextMenuContributor — 몸 컨텍스트 메뉴 Contributor
// ============================================================

using System.Collections.Generic;

public interface ICharacterBodyContextMenuContributor
{
    void Contribute(CharacterBodyContextRequest request, List<ContextMenuEntry> roots);
}
