document.getElementById('search-form').addEventListener('submit', async (e) => {
  e.preventDefault();
  const q = document.getElementById('q').value.trim();
  if (!q) return;

  const [gamesResp, reviewsResp] = await Promise.all([
    fetch(`/api/search/games?q=${encodeURIComponent(q)}`),
    fetch(`/api/search/reviews?q=${encodeURIComponent(q)}`)
  ]);

  const games = document.getElementById('games');
  const reviews = document.getElementById('reviews');
  games.innerHTML = '';
  reviews.innerHTML = '';

  if (gamesResp.ok) {
    const json = await gamesResp.json();
    const items = json?.data?.items ?? [];
    for (const g of items) {
      const li = document.createElement('li');
      li.className = 'list-group-item';
      li.textContent = `${g.gameTitle ?? g.title ?? g.gameId}`;
      games.appendChild(li);
    }
  }

  if (reviewsResp.ok) {
    const json = await reviewsResp.json();
    const items = json?.data?.items ?? [];
    for (const r of items) {
      const li = document.createElement('li');
      li.className = 'list-group-item';
      li.textContent = `${r.excerpt ?? r.reviewId}`;
      reviews.appendChild(li);
    }
  }
});
